using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;
using PoolMate.Api.Integrations.Email;
using OfficeOpenXml;
using System.IO;
using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Services;

public class TournamentPlayerService : ITournamentPlayerService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;

    public TournamentPlayerService(ApplicationDbContext db, IEmailSender emailSender)
    {
        _db = db;
        _emailSender = emailSender;
    }

    private static bool CanEditBracket(Tournament t)
        => !(t.IsStarted || t.Status == TournamentStatus.InProgress || t.Status == TournamentStatus.Completed);

    private async Task<(bool CanAdd, int CurrentCount, int? MaxLimit)> CanAddPlayersAsync(
        int tournamentId,
        int playersToAdd,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);

        if (tournament?.BracketSizeEstimate == null)
        {
            return (true, 0, null);
        }

        var currentCount = await _db.TournamentPlayers
            .CountAsync(x => x.TournamentId == tournamentId, ct);

        var maxLimit = tournament.BracketSizeEstimate ?? 256;
        var canAdd = (currentCount + playersToAdd) <= maxLimit;

        return (canAdd, currentCount, maxLimit);
    }

    private async Task ValidateSeedAsync(int tournamentId, int? seed, int? excludeTpId, CancellationToken ct)
    {
        if (!seed.HasValue) return;

        if (seed.Value <= 0)
            throw new InvalidOperationException("Seed must be a positive number.");

        var existingPlayer = await _db.TournamentPlayers
            .Where(x => x.TournamentId == tournamentId &&
                        x.Seed == seed.Value &&
                        (excludeTpId == null || x.Id != excludeTpId))
            .FirstOrDefaultAsync(ct);

        if (existingPlayer is not null)
        {
            throw new InvalidOperationException(
                $"Seed {seed.Value} is already assigned to player '{existingPlayer.DisplayName}' in this tournament.");
        }
    }

    public async Task<TournamentPlayer?> AddTournamentPlayerAsync(
        int tournamentId,
        string ownerUserId,
        AddTournamentPlayerModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return null;

        if (!CanEditBracket(tournament))
        {
            throw new InvalidOperationException("Cannot add players after tournament has started or completed.");
        }

        var (canAdd, currentCount, maxLimit) = await CanAddPlayersAsync(tournamentId, 1, ct);
        if (!canAdd)
        {
            throw new InvalidOperationException(
                $"Cannot add player. Tournament is full ({currentCount}/{maxLimit}).");
        }

        if (model.PlayerId.HasValue)
        {
            var exists = await _db.TournamentPlayers
                .AnyAsync(x => x.TournamentId == tournamentId && x.PlayerId == model.PlayerId, ct);
            if (exists) throw new InvalidOperationException("This player is already in the tournament.");
        }

        await ValidateSeedAsync(tournamentId, model.Seed, null, ct);

        var tp = new TournamentPlayer
        {
            TournamentId = tournamentId,
            PlayerId = model.PlayerId,
            DisplayName = model.DisplayName.Trim(),
            Nickname = model.Nickname,
            Email = model.Email,
            Phone = model.Phone,
            City = model.City,
            Country = model.Country,
            SkillLevel = model.SkillLevel,
            Seed = model.Seed,
            Status = TournamentPlayerStatus.Confirmed
        };

        _db.TournamentPlayers.Add(tp);
        await _db.SaveChangesAsync(ct);
        return tp;
    }

    public async Task<BulkAddPlayersResult> BulkAddPlayersPerLineAsync(
        int tournamentId,
        string ownerUserId,
        AddTournamentPlayersPerLineModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);

        if (tournament is null) throw new InvalidOperationException("Tournament not found.");
        if (tournament.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException();

        if (!CanEditBracket(tournament))
        {
            throw new InvalidOperationException("Cannot add players after tournament has started or completed.");
        }

        var lines = (model.Lines ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(x => x.Trim())
            .ToList();

        var result = new BulkAddPlayersResult();
        if (lines.Count == 0) return result;

        var (canAdd, currentCount, maxLimit) = await CanAddPlayersAsync(tournamentId, lines.Count, ct);
        if (!canAdd)
        {
            var availableSlots = maxLimit.HasValue ? Math.Max(0, maxLimit.Value - currentCount) : lines.Count;
            throw new InvalidOperationException(
                $"Cannot add {lines.Count} players. Tournament has {availableSlots} available slots ({currentCount}/{maxLimit}).");
        }

        var toAdd = new List<TournamentPlayer>(capacity: lines.Count);
        var defaultStatus = TournamentPlayerStatus.Confirmed;

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                result.Skipped.Add(new BulkAddPlayersResult.SkippedItem
                {
                    Line = raw,
                    Reason = "Empty line"
                });
                continue;
            }

            var name = raw.Trim();
            if (name.Length > 200)
            {
                name = name[..200];
            }

            toAdd.Add(new TournamentPlayer
            {
                TournamentId = tournamentId,
                DisplayName = name,
                Status = defaultStatus,
            });
        }

        if (toAdd.Count == 0) return result;

        _db.TournamentPlayers.AddRange(toAdd);
        await _db.SaveChangesAsync(ct);

        result.AddedCount = toAdd.Count;
        result.Added = toAdd
            .Select(x => new BulkAddPlayersResult.Item
            {
                Id = x.Id,
                DisplayName = x.DisplayName
            })
            .ToList();

        return result;
    }

    public async Task<BulkAddPlayersResult> BulkAddPlayersFromExcelAsync(
        int tournamentId,
        string ownerUserId,
        Stream fileStream,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);

        if (tournament is null) throw new InvalidOperationException("Tournament not found.");
        if (tournament.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException();

        if (!CanEditBracket(tournament))
        {
            throw new InvalidOperationException("Cannot add players after tournament has started or completed.");
        }

        var result = new BulkAddPlayersResult();
        List<TournamentPlayer> toAdd;

        try
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(fileStream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();

            if (worksheet == null || worksheet.Dimension == null)
                throw new InvalidOperationException("Invalid file: Excel file is empty or has no data.");

            // Validate structure - check headers
            var headers = new[]
            {
                "DisplayName", "Nickname", "Email", "Phone", 
                "Country", "City", "SkillLevel", "Seed"
            };

            for (int col = 1; col <= headers.Length; col++)
            {
                var cellValue = worksheet.Cells[1, col].Text?.Trim();
                if (!string.Equals(cellValue, headers[col - 1], StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Invalid structure: Column {col} header must be '{headers[col - 1]}' but found '{cellValue}'.");
                }
            }

            // Parse rows
            var players = new List<TournamentPlayer>();
            var rowCount = worksheet.Dimension.End.Row;

            if (rowCount < 2)
                throw new InvalidOperationException("Invalid file: No player data (only headers).");

            for (int row = 2; row <= rowCount; row++)
            {
                var displayName = worksheet.Cells[row, 1].Text?.Trim();
                if (string.IsNullOrWhiteSpace(displayName)) continue; // Skip empty rows

                var nickname = worksheet.Cells[row, 2].Text?.Trim();
                var email = worksheet.Cells[row, 3].Text?.Trim();
                var phone = worksheet.Cells[row, 4].Text?.Trim();
                var country = worksheet.Cells[row, 5].Text?.Trim();
                var city = worksheet.Cells[row, 6].Text?.Trim();
                var skillLevelText = worksheet.Cells[row, 7].Text?.Trim();
                var seedText = worksheet.Cells[row, 8].Text?.Trim();

                // Validate email format
                if (!string.IsNullOrWhiteSpace(email))
                {
                    var emailAttr = new EmailAddressAttribute();
                    if (!emailAttr.IsValid(email))
                        throw new InvalidOperationException("Invalid data: Invalid email format.");
                }

                // Validate country code
                if (!string.IsNullOrWhiteSpace(country) && country.Length != 2)
                    throw new InvalidOperationException("Invalid data: Country code must be 2 characters.");

                // Parse skill level
                int? skillLevel = null;
                if (!string.IsNullOrWhiteSpace(skillLevelText))
                {
                    if (!int.TryParse(skillLevelText, out var sl) || sl < 0)
                        throw new InvalidOperationException("Invalid data: SkillLevel must be a non-negative number.");
                    skillLevel = sl;
                }

                // Parse seed
                int? seed = null;
                if (!string.IsNullOrWhiteSpace(seedText))
                {
                    if (!int.TryParse(seedText, out var s) || s <= 0)
                        throw new InvalidOperationException("Invalid data: Seed must be a positive number.");
                    seed = s;
                }

                var player = new TournamentPlayer
                {
                    TournamentId = tournamentId,
                    DisplayName = displayName,
                    Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname,
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    Country = string.IsNullOrWhiteSpace(country) ? null : country,
                    City = string.IsNullOrWhiteSpace(city) ? null : city,
                    SkillLevel = skillLevel,
                    Seed = seed,
                    Status = TournamentPlayerStatus.Confirmed
                };

                players.Add(player);
            }

            if (players.Count == 0)
                throw new InvalidOperationException("Invalid file: No valid players in file.");

            toAdd = players;
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw validation errors
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid file: Cannot read Excel file. {ex.Message}");
        }

        // Check capacity
        var (canAdd, currentCount, maxLimit) = await CanAddPlayersAsync(tournamentId, toAdd.Count, ct);
        if (!canAdd)
        {
            var availableSlots = maxLimit.HasValue ? Math.Max(0, maxLimit.Value - currentCount) : toAdd.Count;
            throw new InvalidOperationException(
                $"Invalid data: Cannot add {toAdd.Count} players. Tournament has only {availableSlots} slots remaining ({currentCount}/{maxLimit}).");
        }

        // Check duplicate seeds
        var seedsInFile = toAdd.Where(p => p.Seed.HasValue).Select(p => p.Seed!.Value).ToList();
        if (seedsInFile.Count != seedsInFile.Distinct().Count())
            throw new InvalidOperationException("Invalid data: Duplicate seeds in file.");

        // Check seeds against existing players
        if (seedsInFile.Any())
        {
            var existingSeeds = await _db.TournamentPlayers
                .Where(x => x.TournamentId == tournamentId && x.Seed.HasValue && seedsInFile.Contains(x.Seed.Value))
                .Select(x => x.Seed!.Value)
                .ToListAsync(ct);

            if (existingSeeds.Any())
                throw new InvalidOperationException("Invalid data: Seed already exists in tournament.");
        }

        // Add players
        _db.TournamentPlayers.AddRange(toAdd);
        await _db.SaveChangesAsync(ct);

        result.AddedCount = toAdd.Count;
        result.Added = toAdd
            .Select(x => new BulkAddPlayersResult.Item
            {
                Id = x.Id,
                DisplayName = x.DisplayName
            })
            .ToList();

        return result;
    }

    public byte[] GeneratePlayersTemplate()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Players");

        // Headers
        worksheet.Cells[1, 1].Value = "DisplayName";
        worksheet.Cells[1, 2].Value = "Nickname";
        worksheet.Cells[1, 3].Value = "Email";
        worksheet.Cells[1, 4].Value = "Phone";
        worksheet.Cells[1, 5].Value = "Country";
        worksheet.Cells[1, 6].Value = "City";
        worksheet.Cells[1, 7].Value = "SkillLevel";
        worksheet.Cells[1, 8].Value = "Seed";

        // Style headers
        using (var range = worksheet.Cells[1, 1, 1, 8])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        }

        // Sample data (8 rows)
        var samples = new[]
        {
            new { Name = "Nguyễn Văn A", Nick = "Ace", Email = "nguyenvana@example.com", Phone = "0901234567", Country = "VN", City = "Hà Nội", Skill = "2800", Seed = "1" },
            new { Name = "Trần Thị B", Nick = "Queen", Email = "tranthib@example.com", Phone = "0912345678", Country = "VN", City = "TP.HCM", Skill = "2650", Seed = "2" },
            new { Name = "Lê Văn C", Nick = "King", Email = "levanc@example.com", Phone = "0923456789", Country = "VN", City = "Đà Nẵng", Skill = "2500", Seed = "3" },
            new { Name = "Phạm Thị D", Nick = "", Email = "phamthid@example.com", Phone = "", Country = "VN", City = "Cần Thơ", Skill = "2400", Seed = "" },
            new { Name = "Hoàng Văn E", Nick = "Shark", Email = "", Phone = "0945678901", Country = "VN", City = "Hải Phòng", Skill = "", Seed = "5" },
            new { Name = "Võ Thị F", Nick = "", Email = "vothif@example.com", Phone = "", Country = "VN", City = "Huế", Skill = "2200", Seed = "" },
            new { Name = "Đỗ Văn G", Nick = "Tiger", Email = "", Phone = "0967890123", Country = "VN", City = "Nha Trang", Skill = "", Seed = "" },
            new { Name = "Bùi Thị H", Nick = "", Email = "buithih@example.com", Phone = "", Country = "VN", City = "Vũng Tàu", Skill = "2000", Seed = "8" }
        };

        for (int i = 0; i < samples.Length; i++)
        {
            var row = i + 2;
            var sample = samples[i];
            worksheet.Cells[row, 1].Value = sample.Name;
            worksheet.Cells[row, 2].Value = sample.Nick;
            worksheet.Cells[row, 3].Value = sample.Email;
            worksheet.Cells[row, 4].Value = sample.Phone;
            worksheet.Cells[row, 5].Value = sample.Country;
            worksheet.Cells[row, 6].Value = sample.City;
            worksheet.Cells[row, 7].Value = sample.Skill;
            worksheet.Cells[row, 8].Value = sample.Seed;
        }

        // Auto-fit columns
        worksheet.Cells.AutoFitColumns();

        return package.GetAsByteArray();
    }

    public async Task<List<PlayerSearchItemDto>> SearchPlayersAsync(string q, int limit, CancellationToken ct)
    {
        q = (q ?? string.Empty).Trim();
        if (limit <= 0 || limit > 50) limit = 10;

        var query = _db.Set<Player>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var qLower = q.ToLower();
            query = query.Where(p =>
                p.FullName.ToLower().Contains(qLower));
        }

        var items = await query
            .OrderBy(p => p.FullName)
            .Take(limit)
            .Select(p => new PlayerSearchItemDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Email = p.Email,
                Phone = p.Phone,
                Country = p.Country,
                City = p.City,
                SkillLevel = p.SkillLevel
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<bool> LinkTournamentPlayerAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        LinkPlayerRequest request,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return false;

        var tp = await _db.TournamentPlayers.FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return false;

        var player = await _db.Set<Player>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.PlayerId, ct);
        if (player is null) return false;

        tp.PlayerId = player.Id;

        if (request.OverwriteSnapshot)
        {
            tp.DisplayName = player.FullName;
            tp.Email = player.Email;
            tp.Phone = player.Phone;
            tp.Country = player.Country;
            tp.City = player.City;
            tp.SkillLevel = player.SkillLevel;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UnlinkTournamentPlayerAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return false;

        var tp = await _db.TournamentPlayers
            .Include(tp => tp.Player)
            .FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return false;

        // Prevent unlinking if player self-registered (has a linked user account)
        if (tp.Player?.UserId != null)
        {
            throw new InvalidOperationException(
                "Cannot unlink this player profile. This player self-registered with their account and the profile link cannot be removed.");
        }

        tp.PlayerId = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int?> CreateProfileFromSnapshotAndLinkAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        CreateProfileFromSnapshotRequest request,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return null;

        var tp = await _db.TournamentPlayers.FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return null;

        string baseSlug = SlugHelper.GenerateSlug(tp.DisplayName);
        string finalSlug = baseSlug;
        int count = 1;
        while (await _db.Players.AsNoTracking().AnyAsync(p => p.Slug == finalSlug, ct))
        {
            finalSlug = $"{baseSlug}-{count}";
            count++;
        }

        var player = new Player
        {
            FullName = tp.DisplayName,
            Slug = finalSlug,
            Email = tp.Email,
            Phone = tp.Phone,
            Country = tp.Country,
            City = tp.City,
            SkillLevel = tp.SkillLevel,
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<Player>().Add(player);
        await _db.SaveChangesAsync(ct);

        tp.PlayerId = player.Id;

        if (request.CopyBackToSnapshot)
        {
            tp.DisplayName = player.FullName;
            tp.Email = player.Email;
            tp.Phone = player.Phone;
            tp.Country = player.Country;
            tp.City = player.City;
            tp.SkillLevel = player.SkillLevel;
        }

        await _db.SaveChangesAsync(ct);
        return player.Id;
    }

    public async Task<List<TournamentPlayerListDto>> GetTournamentPlayersAsync(
        int tournamentId,
        string? searchName,
        CancellationToken ct)
    {
        var query = _db.TournamentPlayers
            .AsNoTracking()
            .Where(x => x.TournamentId == tournamentId);

        if (!string.IsNullOrWhiteSpace(searchName))
        {
            var trimmedSearch = searchName.Trim().ToLower();
            query = query.Where(x => x.DisplayName.ToLower().Contains(trimmedSearch));
        }

        var items = await query
            .OrderBy(x => x.Seed ?? int.MaxValue)
            .ThenBy(x => x.DisplayName)
            .Select(x => new TournamentPlayerListDto
            {
                Id = x.Id,
                DisplayName = x.DisplayName,
                Email = x.Email,
                Phone = x.Phone,
                Country = x.Country,
                Seed = x.Seed,
                SkillLevel = x.SkillLevel,
                Status = x.Status,
                PlayerId = x.PlayerId
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<bool> UpdateTournamentPlayerAsync(
        int tournamentId,
        int tpId,
        string ownerUserId,
        UpdateTournamentPlayerModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return false;

        var tp = await _db.TournamentPlayers
            .FirstOrDefaultAsync(x => x.Id == tpId && x.TournamentId == tournamentId, ct);
        if (tp is null) return false;

        if (!CanEditBracket(tournament))
        {
            throw new InvalidOperationException("Cannot modify players after tournament has started or completed.");
        }

        if (model.Seed != tp.Seed)
        {
            await ValidateSeedAsync(tournamentId, model.Seed, tpId, ct);
        }

        if (!string.IsNullOrWhiteSpace(model.DisplayName))
            tp.DisplayName = model.DisplayName.Trim();

        if (model.Nickname != null)
            tp.Nickname = model.Nickname.Trim();

        if (model.Email != null)
            tp.Email = model.Email.Trim();

        if (model.Phone != null)
            tp.Phone = model.Phone.Trim();

        if (model.Country != null)
            tp.Country = model.Country.Trim();

        if (model.City != null)
            tp.City = model.City.Trim();

        tp.SkillLevel = model.SkillLevel;
        tp.Seed = model.Seed;

        if (model.Status.HasValue)
            tp.Status = model.Status.Value;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DeletePlayersResult?> DeleteTournamentPlayersAsync(
        int tournamentId,
        string ownerUserId,
        DeletePlayersModel model,
        CancellationToken ct)
    {
        var tournament = await _db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);
        if (tournament is null || tournament.OwnerUserId != ownerUserId) return null;

        if (!CanEditBracket(tournament))
        {
            throw new InvalidOperationException("Cannot delete players after tournament has started or completed.");
        }

        var result = new DeletePlayersResult();

        if (model.PlayerIds.Count == 0) return result;

        var existingPlayers = await _db.TournamentPlayers
            .Where(x => x.TournamentId == tournamentId && model.PlayerIds.Contains(x.Id))
            .ToListAsync(ct);

        var existingIds = existingPlayers.Select(x => x.Id).ToHashSet();

        foreach (var requestedId in model.PlayerIds)
        {
            if (!existingIds.Contains(requestedId))
            {
                result.Failed.Add(new DeletePlayersResult.FailedItem
                {
                    PlayerId = requestedId,
                    Reason = "Player not found or doesn't belong to this tournament"
                });
            }
        }

        if (existingPlayers.Count > 0)
        {
            _db.TournamentPlayers.RemoveRange(existingPlayers);
            await _db.SaveChangesAsync(ct);

            result.DeletedCount = existingPlayers.Count;
            result.DeletedIds = existingPlayers.Select(x => x.Id).ToList();
        }

        return result;
    }

    public async Task<TournamentPlayer> RegisterForTournamentAsync(
        int tournamentId,
        string userId,
        RegisterForTournamentRequest request,
        CancellationToken ct)
    {
        // Check tournament exists and online registration is enabled
        var tournament = await _db.Tournaments
            .Include(t => t.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct);

        if (tournament is null)
            throw new KeyNotFoundException("Tournament not found.");

        if (!tournament.OnlineRegistrationEnabled)
            throw new InvalidOperationException("Online registration is not enabled for this tournament.");

        if (!CanEditBracket(tournament))
            throw new InvalidOperationException("Cannot register after tournament has started or completed.");

        // Check bracket size limit
        var (canAdd, currentCount, maxLimit) = await CanAddPlayersAsync(tournamentId, 1, ct);
        if (!canAdd)
        {
            throw new InvalidOperationException(
                $"Tournament is full ({currentCount}/{maxLimit}). Registration closed.");
        }

        // User MUST have a Player profile to self-register
        var existingPlayer = await _db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (existingPlayer == null)
        {
            throw new InvalidOperationException(
                "You must create a player profile before registering for tournaments. Please complete your profile first.");
        }

        // Check if already registered using PlayerId (duplicate prevention)
        var alreadyInTournament = await _db.TournamentPlayers
            .AnyAsync(tp => tp.TournamentId == tournamentId && tp.PlayerId == existingPlayer.Id, ct);
        
        if (alreadyInTournament)
            throw new InvalidOperationException("You have already registered for this tournament.");

        // Create tournament player entry with linked Player profile
        var tournamentPlayer = new TournamentPlayer
        {
            TournamentId = tournamentId,
            PlayerId = existingPlayer.Id, // Always set - required Player profile
            DisplayName = existingPlayer.FullName,
            Nickname = existingPlayer.Nickname,
            Email = existingPlayer.Email,
            Phone = existingPlayer.Phone,
            Country = existingPlayer.Country,
            City = existingPlayer.City,
            SkillLevel = existingPlayer.SkillLevel,
            Status = TournamentPlayerStatus.Unconfirmed
        };

        _db.TournamentPlayers.Add(tournamentPlayer);
        await _db.SaveChangesAsync(ct);

        // Send email notification to organizer
        try
        {
            var organizerEmail = await GetOrganizerEmailAsync(tournament.OwnerUserId, ct);
            if (!string.IsNullOrEmpty(organizerEmail))
            {
                var subject = $"New Registration for {tournament.Name}";
                var body = $@"
                    <h2>New Player Registration</h2>
                    <p>A new player has registered for your tournament <strong>{tournament.Name}</strong>.</p>
                    <h3>Player Details:</h3>
                    <ul>
                        <li><strong>Name:</strong> {tournamentPlayer.DisplayName}</li>
                        <li><strong>Nickname:</strong> {tournamentPlayer.Nickname ?? "N/A"}</li>
                        <li><strong>Email:</strong> {tournamentPlayer.Email ?? "N/A"}</li>
                        <li><strong>Phone:</strong> {tournamentPlayer.Phone ?? "N/A"}</li>
                        <li><strong>Country:</strong> {tournamentPlayer.Country ?? "N/A"}</li>
                        <li><strong>City:</strong> {tournamentPlayer.City ?? "N/A"}</li>
                        <li><strong>Profile Status:</strong> Linked (cannot be unlinked)</li>
                    </ul>
                    <p><strong>Note:</strong> This player self-registered with their linked profile. The profile connection cannot be removed.</p>
                    <p>Please review and approve the registration in your tournament management dashboard.</p>
                ";

                await _emailSender.SendAsync(organizerEmail, subject, body, ct);
            }
        }
        catch (Exception)
        {
            
        }

        return tournamentPlayer;
    }

    private async Task<string?> GetOrganizerEmailAsync(string ownerUserId, CancellationToken ct)
    {
        // Try to get organizer email first (business email)
        var organizer = await _db.Organizers
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.UserId == ownerUserId, ct);

        if (organizer != null && !string.IsNullOrEmpty(organizer.Email))
            return organizer.Email;

        // Fallback to user's personal email
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == ownerUserId, ct);

        return user?.Email;
    }
}

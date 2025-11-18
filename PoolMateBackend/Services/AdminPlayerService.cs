using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Common;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Admin.Player;
using PoolMate.Api.Dtos.Auth;
using PoolMate.Api.Dtos.Player;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services;

public class AdminPlayerService : IAdminPlayerService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminPlayerService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // ===== Admin APIs =====

    public async Task<PagingList<PlayerListDto>> GetPlayersAsync(PlayerFilterDto filter, CancellationToken ct = default)
    {
        // Start with base query
        var query = _db.Players
            .AsNoTracking()
            .Include(p => p.User)
            .AsQueryable();

        // Apply filters
        
        // Search by full name (partial match, case-insensitive)
        if (!string.IsNullOrWhiteSpace(filter.SearchName))
        {
            var searchTerm = filter.SearchName.ToLower();
            query = query.Where(p => p.FullName.ToLower().Contains(searchTerm));
        }

        // 🆕 Search by email (partial match, case-insensitive)
        if (!string.IsNullOrWhiteSpace(filter.SearchEmail))
        {
            var searchEmail = filter.SearchEmail.ToLower();
            query = query.Where(p => p.Email != null && p.Email.ToLower().Contains(searchEmail));
        }

        // 🆕 Search by phone (partial match)
        if (!string.IsNullOrWhiteSpace(filter.SearchPhone))
        {
            var searchPhone = filter.SearchPhone;
            query = query.Where(p => p.Phone != null && p.Phone.Contains(searchPhone));
        }

        // 🆕 Search by tournament name (players who participated)
        if (!string.IsNullOrWhiteSpace(filter.SearchTournament))
        {
            var searchTournament = filter.SearchTournament.ToLower();
            var playerIdsInTournament = _db.TournamentPlayers
                .Where(tp => tp.Tournament != null && tp.Tournament.Name.ToLower().Contains(searchTournament))
                .Select(tp => tp.PlayerId)
                .Distinct();
            query = query.Where(p => playerIdsInTournament.Contains(p.Id));
        }

        // Filter by country
        if (!string.IsNullOrWhiteSpace(filter.Country))
        {
            query = query.Where(p => p.Country == filter.Country);
        }

        // Filter by city
        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            query = query.Where(p => p.City == filter.City);
        }

        // Filter by skill level range
        if (filter.MinSkillLevel.HasValue)
        {
            query = query.Where(p => p.SkillLevel >= filter.MinSkillLevel.Value);
        }

        if (filter.MaxSkillLevel.HasValue)
        {
            query = query.Where(p => p.SkillLevel <= filter.MaxSkillLevel.Value);
        }

        // Filter by linked account status
        if (filter.HasLinkedAccount.HasValue)
        {
            if (filter.HasLinkedAccount.Value)
            {
                query = query.Where(p => p.UserId != null);
            }
            else
            {
                query = query.Where(p => p.UserId == null);
            }
        }

        // 🆕 Date range filters
        if (filter.CreatedFrom.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= filter.CreatedFrom.Value);
        }

        if (filter.CreatedTo.HasValue)
        {
            var toDate = filter.CreatedTo.Value.AddDays(1); // Include the end date
            query = query.Where(p => p.CreatedAt < toDate);
        }

        // 🆕 Last tournament date filters (requires subquery)
        if (filter.LastTournamentFrom.HasValue || filter.LastTournamentTo.HasValue)
        {
            var playersWithTournaments = _db.TournamentPlayers
                .Include(tp => tp.Tournament)
                .GroupBy(tp => tp.PlayerId)
                .Select(g => new
                {
                    PlayerId = g.Key,
                    LastTournamentDate = g.Max(tp => tp.Tournament!.StartUtc)
                });

            if (filter.LastTournamentFrom.HasValue)
            {
                var playerIds = playersWithTournaments
                    .Where(pt => pt.LastTournamentDate >= filter.LastTournamentFrom.Value)
                    .Select(pt => pt.PlayerId);
                query = query.Where(p => playerIds.Contains(p.Id));
            }

            if (filter.LastTournamentTo.HasValue)
            {
                var playerIds = playersWithTournaments
                    .Where(pt => pt.LastTournamentDate <= filter.LastTournamentTo.Value)
                    .Select(pt => pt.PlayerId);
                query = query.Where(p => playerIds.Contains(p.Id));
            }
        }

        // 🆕 Data quality filters
        if (filter.HasEmail.HasValue)
        {
            if (filter.HasEmail.Value)
            {
                query = query.Where(p => p.Email != null && p.Email != "");
            }
            else
            {
                query = query.Where(p => p.Email == null || p.Email == "");
            }
        }

        if (filter.HasPhone.HasValue)
        {
            if (filter.HasPhone.Value)
            {
                query = query.Where(p => p.Phone != null && p.Phone != "");
            }
            else
            {
                query = query.Where(p => p.Phone == null || p.Phone == "");
            }
        }

        if (filter.HasSkillLevel.HasValue)
        {
            if (filter.HasSkillLevel.Value)
            {
                query = query.Where(p => p.SkillLevel != null);
            }
            else
            {
                query = query.Where(p => p.SkillLevel == null);
            }
        }

        // Get total count before pagination
        var totalRecords = await query.CountAsync(ct);

        // Apply sorting
        query = ApplySorting(query, filter.SortBy, filter.SortOrder);

        // Apply pagination
        var items = await query
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new PlayerListDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Nickname = p.Nickname,
                Email = p.Email,
                Phone = p.Phone,
                Country = p.Country,
                City = p.City,
                SkillLevel = p.SkillLevel,
                CreatedAt = p.CreatedAt,
                LinkedUserId = p.UserId,
                LinkedUserEmail = p.User != null ? p.User.Email : null
            })
            .ToListAsync(ct);

        // Return paginated result
        return PagingList<PlayerListDto>.Create(items, totalRecords, filter.PageIndex, filter.PageSize);
    }

    public async Task<PlayerDetailDto?> GetPlayerDetailAsync(int playerId, CancellationToken ct = default)
    {
        var player = await _db.Players
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.TournamentPlayers)
                .ThenInclude(tp => tp.Tournament)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (player == null) return null;

        // Map basic info
        var result = new PlayerDetailDto
        {
            Id = player.Id,
            FullName = player.FullName,
            Nickname = player.Nickname,
            Email = player.Email,
            Phone = player.Phone,
            Country = player.Country,
            City = player.City,
            SkillLevel = player.SkillLevel,
            CreatedAt = player.CreatedAt,
            HasLinkedAccount = player.UserId != null
        };

        // Map linked user info
        if (player.User != null)
        {
            result.LinkedUser = new LinkedUserDetailDto
            {
                UserId = player.UserId!,
                Email = player.User.Email,
                UserName = player.User.UserName,
                FullName = string.IsNullOrWhiteSpace(player.User.FirstName) && string.IsNullOrWhiteSpace(player.User.LastName)
                    ? null
                    : $"{player.User.FirstName} {player.User.LastName}".Trim(),
                Nickname = player.User.Nickname,
                PhoneNumber = player.User.PhoneNumber,
                ProfilePicture = player.User.ProfilePicture,
                CreatedAt = player.User.CreatedAt
            };
        }

        // Calculate tournament statistics
        var tournaments = player.TournamentPlayers.ToList();
        var tournamentDates = tournaments
            .Where(tp => tp.Tournament != null)
            .Select(tp => tp.Tournament.StartUtc)
            .OrderBy(d => d)
            .ToList();

        result.TournamentStats = new TournamentStatsDto
        {
            TotalTournaments = tournaments.Count,
            CompletedTournaments = tournaments.Count(tp => tp.Tournament != null && tp.Tournament.EndUtc.HasValue && tp.Tournament.EndUtc.Value < DateTime.UtcNow),
            ActiveTournaments = tournaments.Count(tp => tp.Tournament != null && 
                tp.Tournament.StartUtc <= DateTime.UtcNow && 
                (!tp.Tournament.EndUtc.HasValue || tp.Tournament.EndUtc.Value >= DateTime.UtcNow)),
            FirstTournamentDate = tournamentDates.FirstOrDefault(),
            LastTournamentDate = tournamentDates.LastOrDefault()
        };

        // Map recent tournaments (top 10, newest first)
        result.RecentTournaments = tournaments
            .Where(tp => tp.Tournament != null)
            .OrderByDescending(tp => tp.Tournament.StartUtc)
            .Take(10)
            .Select(tp => new PlayerTournamentHistoryDto
            {
                TournamentId = tp.TournamentId,
                TournamentName = tp.Tournament.Name,
                TournamentDate = tp.Tournament.StartUtc,
                TournamentStatus = GetTournamentStatus(tp.Tournament),
                Seed = tp.Seed,
                PlayerStatus = tp.Status.ToString()
            })
            .ToList();

        return result;
    }


    public async Task<PlayerStatisticsDto> GetPlayerStatisticsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);

        // Get all players with related data
        var allPlayers = await _db.Players
            .AsNoTracking()
            .Include(p => p.TournamentPlayers)
            .ToListAsync(ct);

        var totalPlayers = allPlayers.Count;

        // Basic statistics
        var playersWithLinkedAccount = allPlayers.Count(p => p.UserId != null);
        var playersWithoutLinkedAccount = totalPlayers - playersWithLinkedAccount;
        var linkedAccountPercentage = totalPlayers > 0 
            ? Math.Round((double)playersWithLinkedAccount / totalPlayers * 100, 2) 
            : 0;

        // Recent activity
        var playersCreatedLast30Days = allPlayers.Count(p => p.CreatedAt >= last30Days);
        var playersCreatedLast7Days = allPlayers.Count(p => p.CreatedAt >= last7Days);
        var playersCreatedToday = allPlayers.Count(p => p.CreatedAt.Date == today);

        // Skill level distribution
        var skillLevelGroups = allPlayers
            .GroupBy(p => p.SkillLevel)
            .Select(g => new SkillLevelDistributionDto
            {
                SkillLevel = g.Key,
                Count = g.Count(),
                Percentage = totalPlayers > 0 
                    ? Math.Round((double)g.Count() / totalPlayers * 100, 2) 
                    : 0
            })
            .OrderBy(x => x.SkillLevel ?? int.MaxValue)
            .ToList();

        // Geographic distribution - Top 10 countries
        var topCountries = allPlayers
            .Where(p => !string.IsNullOrWhiteSpace(p.Country))
            .GroupBy(p => p.Country)
            .Select(g => new GeographicDistributionDto
            {
                Location = g.Key!,
                Count = g.Count(),
                Percentage = totalPlayers > 0 
                    ? Math.Round((double)g.Count() / totalPlayers * 100, 2) 
                    : 0
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        // Geographic distribution - Top 10 cities
        var topCities = allPlayers
            .Where(p => !string.IsNullOrWhiteSpace(p.City))
            .GroupBy(p => p.City)
            .Select(g => new GeographicDistributionDto
            {
                Location = g.Key!,
                Count = g.Count(),
                Percentage = totalPlayers > 0 
                    ? Math.Round((double)g.Count() / totalPlayers * 100, 2) 
                    : 0
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        // Tournament activity
        var playersWithTournaments = allPlayers.Count(p => p.TournamentPlayers.Any());
        var playersWithoutTournaments = totalPlayers - playersWithTournaments;

        // Active players (có tournament trong 30 ngày qua)
        var activePlayersLast30Days = allPlayers
            .Count(p => p.TournamentPlayers.Any(tp => 
                tp.Tournament != null && 
                tp.Tournament.StartUtc >= last30Days));

        // Monthly growth trend (last 6 months)
        var monthlyGrowth = new List<PlayerGrowthTrendDto>();
        for (int i = 5; i >= 0; i--)
        {
            var monthStart = now.AddMonths(-i).Date;
            var monthStartFirstDay = new DateTime(monthStart.Year, monthStart.Month, 1);
            var monthEnd = monthStartFirstDay.AddMonths(1);

            var newPlayersInMonth = allPlayers.Count(p => 
                p.CreatedAt >= monthStartFirstDay && 
                p.CreatedAt < monthEnd);

            var totalPlayersUpToMonth = allPlayers.Count(p => p.CreatedAt < monthEnd);

            monthlyGrowth.Add(new PlayerGrowthTrendDto
            {
                Year = monthStartFirstDay.Year,
                Month = monthStartFirstDay.Month,
                MonthName = monthStartFirstDay.ToString("MMM yyyy"),
                NewPlayers = newPlayersInMonth,
                TotalPlayers = totalPlayersUpToMonth
            });
        }

        return new PlayerStatisticsDto
        {
            TotalPlayers = totalPlayers,
            PlayersWithLinkedAccount = playersWithLinkedAccount,
            PlayersWithoutLinkedAccount = playersWithoutLinkedAccount,
            LinkedAccountPercentage = linkedAccountPercentage,
            PlayersCreatedLast30Days = playersCreatedLast30Days,
            PlayersCreatedLast7Days = playersCreatedLast7Days,
            PlayersCreatedToday = playersCreatedToday,
            SkillLevelDistribution = skillLevelGroups,
            TopCountries = topCountries,
            TopCities = topCities,
            PlayersWithTournaments = playersWithTournaments,
            PlayersWithoutTournaments = playersWithoutTournaments,
            ActivePlayersLast30Days = activePlayersLast30Days,
            MonthlyGrowth = monthlyGrowth
        };
    }

    private string GetTournamentStatus(Tournament tournament)
    {
        var now = DateTime.UtcNow;
        
        if (tournament.StartUtc > now)
            return "Upcoming";
        
        if (tournament.EndUtc.HasValue && tournament.EndUtc.Value < now)
            return "Completed";
        
        return "InProgress";
    }

    private IQueryable<Player> ApplySorting(IQueryable<Player> query, string sortBy, string sortOrder)
    {
        var isDescending = sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLower() switch
        {
            "createdat" => isDescending 
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
            "fullname" => isDescending
                ? query.OrderByDescending(p => p.FullName)
                : query.OrderBy(p => p.FullName),
            "skilllevel" => isDescending
                ? query.OrderByDescending(p => p.SkillLevel)
                : query.OrderBy(p => p.SkillLevel),
            "country" => isDescending
                ? query.OrderByDescending(p => p.Country)
                : query.OrderBy(p => p.Country),
            "city" => isDescending
                ? query.OrderByDescending(p => p.City)
                : query.OrderBy(p => p.City),
            _ => query.OrderByDescending(p => p.CreatedAt) // Default: newest first
        };
    }

    public async Task<PagingList<UnclaimedPlayerDto>> GetUnclaimedPlayersAsync(PlayerFilterDto filter, CancellationToken ct = default)
    {
        // Start with base query - only unclaimed players
        var query = _db.Players
            .AsNoTracking()
            .Where(p => p.UserId == null)  // Only unclaimed players
            .Include(p => p.TournamentPlayers)
                .ThenInclude(tp => tp.Tournament)
            .AsQueryable();

        // Apply filters (reuse existing filter logic)
        
        // Search by full name
        if (!string.IsNullOrWhiteSpace(filter.SearchName))
        {
            var searchTerm = filter.SearchName.ToLower();
            query = query.Where(p => p.FullName.ToLower().Contains(searchTerm));
        }

        // Filter by country
        if (!string.IsNullOrWhiteSpace(filter.Country))
        {
            query = query.Where(p => p.Country == filter.Country);
        }

        // Filter by city
        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            query = query.Where(p => p.City == filter.City);
        }

        // Filter by skill level range
        if (filter.MinSkillLevel.HasValue)
        {
            query = query.Where(p => p.SkillLevel >= filter.MinSkillLevel.Value);
        }

        if (filter.MaxSkillLevel.HasValue)
        {
            query = query.Where(p => p.SkillLevel <= filter.MaxSkillLevel.Value);
        }

        // Get total count before pagination
        var totalRecords = await query.CountAsync(ct);

        // Apply sorting
        query = ApplySorting(query, filter.SortBy, filter.SortOrder);

        // Apply pagination and get players
        var players = await query
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        // Get all emails from players for potential matching
        var playerEmails = players
            .Where(p => !string.IsNullOrWhiteSpace(p.Email))
            .Select(p => p.Email!.ToLower())
            .Distinct()
            .ToList();

        // Find potential user matches by email
        var potentialUsers = new Dictionary<string, List<ApplicationUser>>();
        if (playerEmails.Any())
        {
            var usersWithMatchingEmails = await _db.Users
                .AsNoTracking()
                .Where(u => playerEmails.Contains(u.Email!.ToLower()))
                .ToListAsync(ct);

            foreach (var user in usersWithMatchingEmails)
            {
                var emailKey = user.Email!.ToLower();
                if (!potentialUsers.ContainsKey(emailKey))
                {
                    potentialUsers[emailKey] = new List<ApplicationUser>();
                }
                potentialUsers[emailKey].Add(user);
            }
        }

        // Map to DTOs
        var items = players.Select(p =>
        {
            var tournaments = p.TournamentPlayers.ToList();
            var tournamentDates = tournaments
                .Where(tp => tp.Tournament != null)
                .Select(tp => tp.Tournament.StartUtc)
                .OrderByDescending(d => d)
                .ToList();

            // Find potential user matches by email
            var matches = new List<PotentialUserMatchDto>();
            if (!string.IsNullOrWhiteSpace(p.Email))
            {
                var emailKey = p.Email.ToLower();
                if (potentialUsers.ContainsKey(emailKey))
                {
                    matches = potentialUsers[emailKey].Select(u => new PotentialUserMatchDto
                    {
                        UserId = u.Id,
                        Email = u.Email!,
                        UserName = u.UserName,
                        FullName = string.IsNullOrWhiteSpace(u.FirstName) && string.IsNullOrWhiteSpace(u.LastName)
                            ? null
                            : $"{u.FirstName} {u.LastName}".Trim(),
                        CreatedAt = u.CreatedAt
                    }).ToList();
                }
            }

            return new UnclaimedPlayerDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Nickname = p.Nickname,
                Email = p.Email,
                Phone = p.Phone,
                Country = p.Country,
                City = p.City,
                SkillLevel = p.SkillLevel,
                CreatedAt = p.CreatedAt,
                TournamentsCount = tournaments.Count,
                LastTournamentDate = tournamentDates.FirstOrDefault(),
                PotentialMatches = matches
            };
        }).ToList();

        return PagingList<UnclaimedPlayerDto>.Create(items, totalRecords, filter.PageIndex, filter.PageSize);
    }

    public async Task<bool> LinkPlayerToUserAsync(int playerId, string userId, CancellationToken ct = default)
    {
        // Validate Player exists
        var player = await _db.Players.FindAsync(new object[] { playerId }, ct);
        if (player == null) return false;

        // Validate User exists
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        // Check if Player already linked to another User
        if (player.UserId != null && player.UserId != userId)
        {
            // 1 Player chỉ có thể link với 1 User
            // Không cho phép link với User khác
            return false;
        }

        // Link
        player.UserId = userId;
        await _db.SaveChangesAsync(ct);
        
        return true;
    }

    public async Task<bool> UnlinkPlayerFromUserAsync(int playerId, CancellationToken ct = default)
    {
        var player = await _db.Players.FindAsync(new object[] { playerId }, ct);
        if (player == null) return false;

        player.UserId = null;
        await _db.SaveChangesAsync(ct);
        
        return true;
    }

    public async Task<List<PlayerListDto>> GetPlayersByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var players = await _db.Players
            .AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.UserId == userId)
            .Select(p => new PlayerListDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Nickname = p.Nickname,
                Email = p.Email,
                Phone = p.Phone,
                Country = p.Country,
                City = p.City,
                SkillLevel = p.SkillLevel,
                CreatedAt = p.CreatedAt,
                LinkedUserId = p.UserId,
                LinkedUserEmail = p.User != null ? p.User.Email : null
            })
            .ToListAsync(ct);

        return players;
    }

    public async Task<UserInfoDto?> GetLinkedUserAsync(int playerId, CancellationToken ct = default)
    {
        var player = await _db.Players
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (player?.User == null) return null;

        return new UserInfoDto
        {
            UserId = player.UserId!,
            Email = player.User.Email!,
            FullName = string.IsNullOrWhiteSpace(player.User.FirstName) && string.IsNullOrWhiteSpace(player.User.LastName)
                ? null
                : $"{player.User.FirstName} {player.User.LastName}".Trim(),
            Nickname = player.User.Nickname
        };
    }

    /// <summary>
    /// BULK LINK PLAYERS - Link multiple players to users at once
    /// </summary>
    public async Task<BulkOperationResultDto> BulkLinkPlayersAsync(BulkLinkPlayersDto request, CancellationToken ct = default)
    {
        var results = new List<BulkOperationItemDto>();
        var successCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

        foreach (var link in request.Links)
        {
            try
            {
                // Validate Player exists
                var player = await _db.Players
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == link.PlayerId, ct);

                if (player == null)
                {
                    results.Add(new BulkOperationItemDto
                    {
                        PlayerId = link.PlayerId,
                        PlayerName = null,
                        Success = false,
                        ErrorMessage = "Player not found",
                        Status = "Failed"
                    });
                    failedCount++;
                    continue;
                }

                // Validate User exists
                var user = await _userManager.FindByIdAsync(link.UserId);
                if (user == null)
                {
                    results.Add(new BulkOperationItemDto
                    {
                        PlayerId = link.PlayerId,
                        PlayerName = player.FullName,
                        Success = false,
                        ErrorMessage = "User not found",
                        Status = "Failed"
                    });
                    failedCount++;
                    continue;
                }

                // Check if Player already linked to another User
                if (player.UserId != null && player.UserId != link.UserId)
                {
                    results.Add(new BulkOperationItemDto
                    {
                        PlayerId = link.PlayerId,
                        PlayerName = player.FullName,
                        Success = false,
                        ErrorMessage = $"Player already linked to another user ({player.UserId})",
                        Status = "Skipped"
                    });
                    skippedCount++;
                    continue;
                }

                // Check if already linked to this user (idempotent)
                if (player.UserId == link.UserId)
                {
                    results.Add(new BulkOperationItemDto
                    {
                        PlayerId = link.PlayerId,
                        PlayerName = player.FullName,
                        Success = true,
                        ErrorMessage = "Already linked to this user",
                        Status = "Skipped"
                    });
                    skippedCount++;
                    continue;
                }

                // Link
                player.UserId = link.UserId;
                await _db.SaveChangesAsync(ct);

                results.Add(new BulkOperationItemDto
                {
                    PlayerId = link.PlayerId,
                    PlayerName = player.FullName,
                    Success = true,
                    ErrorMessage = null,
                    Status = "Success"
                });
                successCount++;
            }
            catch (Exception ex)
            {
                results.Add(new BulkOperationItemDto
                {
                    PlayerId = link.PlayerId,
                    PlayerName = null,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Status = "Failed"
                });
                failedCount++;
            }
        }

        return new BulkOperationResultDto
        {
            TotalRequested = request.Links.Count,
            SuccessCount = successCount,
            FailedCount = failedCount,
            SkippedCount = skippedCount,
            Results = results,
            ProcessedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// BULK UNLINK PLAYERS - Unlink multiple players from users at once
    /// </summary>
    public async Task<BulkOperationResultDto> BulkUnlinkPlayersAsync(BulkUnlinkPlayersDto request, CancellationToken ct = default)
    {
        var results = new List<BulkOperationItemDto>();
        var successCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

        foreach (var playerId in request.PlayerIds)
        {
            try
            {
                var player = await _db.Players.FindAsync(new object[] { playerId }, ct);

                if (player == null)
                {
                    results.Add(new BulkOperationItemDto
                    {
                        PlayerId = playerId,
                        PlayerName = null,
                        Success = false,
                        ErrorMessage = "Player not found",
                        Status = "Failed"
                    });
                    failedCount++;
                    continue;
                }

                // Check if player is not linked
                if (player.UserId == null)
                {
                    results.Add(new BulkOperationItemDto
                    {
                        PlayerId = playerId,
                        PlayerName = player.FullName,
                        Success = false,
                        ErrorMessage = "Player is not linked to any user",
                        Status = "Skipped"
                    });
                    skippedCount++;
                    continue;
                }

                // Unlink
                player.UserId = null;
                await _db.SaveChangesAsync(ct);

                results.Add(new BulkOperationItemDto
                {
                    PlayerId = playerId,
                    PlayerName = player.FullName,
                    Success = true,
                    ErrorMessage = null,
                    Status = "Success"
                });
                successCount++;
            }
            catch (Exception ex)
            {
                results.Add(new BulkOperationItemDto
                {
                    PlayerId = playerId,
                    PlayerName = null,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Status = "Failed"
                });
                failedCount++;
            }
        }

        return new BulkOperationResultDto
        {
            TotalRequested = request.PlayerIds.Count,
            SuccessCount = successCount,
            FailedCount = failedCount,
            SkippedCount = skippedCount,
            Results = results,
            ProcessedAt = DateTime.UtcNow
        };
    }

    // ===== User Self-Claim APIs =====

    public async Task<ClaimPlayerResponse?> ClaimPlayerAsync(
        int playerId, 
        string userId, 
        bool updateUserProfile = false, 
        CancellationToken ct = default)
    {
        var player = await _db.Players
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);
        
        if (player == null) return null;

        // Already claimed by another user
        if (player.UserId != null && player.UserId != userId)
            return null;

        // Get current user
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return null;

        // Validation: Email must match
        if (string.IsNullOrWhiteSpace(player.Email) || 
            string.IsNullOrWhiteSpace(user.Email) ||
            !player.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return null; // Email không match
        }

        // Claim
        player.UserId = userId;

        // Optional: Update user profile from player data
        if (updateUserProfile)
        {
            if (!string.IsNullOrWhiteSpace(player.Phone))
                user.PhoneNumber = player.Phone;
            
            if (!string.IsNullOrWhiteSpace(player.City))
                user.City = player.City;
            
            if (!string.IsNullOrWhiteSpace(player.Country))
                user.Country = player.Country;

            await _userManager.UpdateAsync(user);
        }

        await _db.SaveChangesAsync(ct);
        
        return new ClaimPlayerResponse
        {
            PlayerId = player.Id,
            FullName = player.FullName,
            Email = player.Email,
            UserId = userId,
            Message = "Player claimed successfully."
        };
    }

    public async Task<List<ClaimablePlayerDto>> GetClaimablePlayersAsync(string userEmail, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userEmail))
            return new List<ClaimablePlayerDto>();

        var players = await _db.Players
            .AsNoTracking()
            .Where(p => p.Email != null && p.Email.ToLower() == userEmail.ToLower())
            .Select(p => new ClaimablePlayerDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Email = p.Email,
                Phone = p.Phone,
                Country = p.Country,
                City = p.City,
                SkillLevel = p.SkillLevel,
                CreatedAt = p.CreatedAt,
                IsClaimed = p.UserId != null,
                EmailMatches = true
            })
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return players;
    }

    public async Task<List<PlayerListDto>> GetMyPlayersAsync(string userId, CancellationToken ct = default)
    {
        // Same as GetPlayersByUserIdAsync but for authenticated user
        return await GetPlayersByUserIdAsync(userId, ct);
    }

    public async Task<DataQualityReportDto> GetDataQualityReportAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var oneYearAgo = now.AddYears(-1);

        var players = await _db.Players
            .AsNoTracking()
            .Include(p => p.TournamentPlayers)
            .ToListAsync(ct);

        int total = players.Count;
        int missingEmail = players.Count(p => string.IsNullOrWhiteSpace(p.Email));
        int missingPhone = players.Count(p => string.IsNullOrWhiteSpace(p.Phone));
        int missingSkill = players.Count(p => p.SkillLevel == null);
        int missingLocation = players.Count(p => string.IsNullOrWhiteSpace(p.Country) || string.IsNullOrWhiteSpace(p.City));

        int invalidEmail = players.Count(p => !string.IsNullOrWhiteSpace(p.Email) && !PlayerDataValidator.IsValidEmail(p.Email));
        int invalidPhone = players.Count(p => !string.IsNullOrWhiteSpace(p.Phone) && !PlayerDataValidator.IsValidPhone(p.Phone));
        int invalidSkill = players.Count(p => p.SkillLevel.HasValue && !PlayerDataValidator.IsValidSkillLevel(p.SkillLevel));

        // Last tournament dates per player
        var lastTournamentByPlayer = players.ToDictionary(
            p => p.Id,
            p => p.TournamentPlayers
                .Where(tp => tp.Tournament != null)
                .Select(tp => (DateTime?)tp.Tournament!.StartUtc)
                .DefaultIfEmpty(null)
                .Max()
        );

        int inactivePlayers = lastTournamentByPlayer.Count(kv => kv.Value.HasValue && kv.Value.Value < oneYearAgo);
        int neverPlayed = lastTournamentByPlayer.Count(kv => !kv.Value.HasValue);

        // Potential duplicates (very simple heuristic: same non-empty email or very similar names)
        int potentialDuplicates = 0;
        var emailGroups = players
            .Where(p => !string.IsNullOrWhiteSpace(p.Email))
            .GroupBy(p => p.Email!.ToLower())
            .Where(g => g.Count() > 1)
            .ToList();
        potentialDuplicates += emailGroups.Count;

        // Name similarity (O(n^2) naive, limit to players created last 3 months to keep cheap)
        var recentPlayers = players.Where(p => p.CreatedAt >= now.AddMonths(-3)).ToList();
        for (int i = 0; i < recentPlayers.Count; i++)
        {
            for (int j = i + 1; j < recentPlayers.Count; j++)
            {
                var a = recentPlayers[i];
                var b = recentPlayers[j];
                if (string.IsNullOrWhiteSpace(a.FullName) || string.IsNullOrWhiteSpace(b.FullName)) continue;
                int sim = PlayerDataValidator.CalculateSimilarity(a.FullName, b.FullName);
                if (sim >= 90) { potentialDuplicates++; }
            }
        }

        var issues = new DataQualityIssuesDto
        {
            MissingEmail = missingEmail,
            MissingPhone = missingPhone,
            MissingSkillLevel = missingSkill,
            MissingLocation = missingLocation,
            InvalidEmail = invalidEmail,
            InvalidPhone = invalidPhone,
            InvalidSkillLevel = invalidSkill,
            InactivePlayers = inactivePlayers,
            NeverPlayedTournament = neverPlayed,
            PotentialDuplicates = potentialDuplicates
        };

        int playersWithAnyIssues = 0;
        foreach (var p in players)
        {
            bool hasIssue = string.IsNullOrWhiteSpace(p.Email)
                || string.IsNullOrWhiteSpace(p.Phone)
                || p.SkillLevel == null
                || string.IsNullOrWhiteSpace(p.Country) || string.IsNullOrWhiteSpace(p.City)
                || (!string.IsNullOrWhiteSpace(p.Email) && !PlayerDataValidator.IsValidEmail(p.Email))
                || (!string.IsNullOrWhiteSpace(p.Phone) && !PlayerDataValidator.IsValidPhone(p.Phone))
                || (p.SkillLevel.HasValue && !PlayerDataValidator.IsValidSkillLevel(p.SkillLevel));
            if (hasIssue) playersWithAnyIssues++;
        }

        var overview = new DataQualityOverviewDto
        {
            TotalPlayers = total,
            PlayersWithIssues = playersWithAnyIssues,
            IssuePercentage = total > 0 ? Math.Round(playersWithAnyIssues * 100.0 / total, 2) : 0,
            HealthyPlayers = total - playersWithAnyIssues,
            HealthyPercentage = total > 0 ? Math.Round((total - playersWithAnyIssues) * 100.0 / total, 2) : 0
        };

        var recommendations = new List<string>();
        if (missingEmail > 0) recommendations.Add($"{missingEmail} players missing email - consider contacting via phone or filling data");
        if (invalidEmail > 0) recommendations.Add($"{invalidEmail} players with invalid email - fix format");
        if (inactivePlayers > 0) recommendations.Add($"{inactivePlayers} players inactive > 1 year - consider archival");
        if (potentialDuplicates > 0) recommendations.Add($"{potentialDuplicates} potential duplicate groups - review merge candidates");

        return new DataQualityReportDto
        {
            Overview = overview,
            Issues = issues,
            Recommendations = recommendations,
            GeneratedAt = now
        };
    }

    public async Task<PlayersWithIssuesDto> GetPlayersWithIssuesAsync(string issueType, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var oneYearAgo = now.AddYears(-1);
        var query = _db.Players
            .AsNoTracking()
            .Include(p => p.TournamentPlayers)
            .AsQueryable();

        IQueryable<Models.Player> filtered = query;
        switch (issueType.ToLower())
        {
            case "missing-email":
                filtered = filtered.Where(p => string.IsNullOrWhiteSpace(p.Email));
                break;
            case "missing-phone":
                filtered = filtered.Where(p => string.IsNullOrWhiteSpace(p.Phone));
                break;
            case "missing-skill":
                filtered = filtered.Where(p => p.SkillLevel == null);
                break;
            case "invalid-email":
                filtered = filtered.Where(p => p.Email != null); // will validate in projection
                break;
            case "invalid-phone":
                filtered = filtered.Where(p => p.Phone != null);
                break;
            case "inactive-1y":
                filtered = filtered.Where(p => p.TournamentPlayers
                    .Where(tp => tp.Tournament != null)
                    .Select(tp => (DateTime?)tp.Tournament!.StartUtc)
                    .DefaultIfEmpty(null)
                    .Max() < oneYearAgo);
                break;
            case "never-played":
                filtered = filtered.Where(p => !p.TournamentPlayers.Any());
                break;
            default:
                // return empty
                filtered = filtered.Where(p => false);
                break;
        }

        var list = await filtered
            .Select(p => new PlayerIssueDto
            {
                Id = p.Id,
                FullName = p.FullName,
                Email = p.Email,
                Phone = p.Phone,
                Issues = new List<string>(),
                CreatedAt = p.CreatedAt,
                LastTournamentDate = p.TournamentPlayers
                    .Where(tp => tp.Tournament != null)
                    .Select(tp => (DateTime?)tp.Tournament!.StartUtc)
                    .DefaultIfEmpty(null)
                    .Max()
            })
            .ToListAsync(ct);

        // Fill issue text for invalid email/phone cases
        foreach (var item in list)
        {
            if (issueType.Equals("invalid-email", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(item.Email) && !PlayerDataValidator.IsValidEmail(item.Email))
                    item.Issues.Add("Invalid email format");
            }
            if (issueType.Equals("invalid-phone", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(item.Phone) && !PlayerDataValidator.IsValidPhone(item.Phone))
                    item.Issues.Add("Invalid phone format");
            }
            if (issueType.Equals("missing-email", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(item.Email)) item.Issues.Add("Missing email");
            }
            if (issueType.Equals("missing-phone", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(item.Phone)) item.Issues.Add("Missing phone");
            }
            if (issueType.Equals("missing-skill", StringComparison.OrdinalIgnoreCase))
            {
                item.Issues.Add("Missing skill level");
            }
            if (issueType.Equals("inactive-1y", StringComparison.OrdinalIgnoreCase))
            {
                item.Issues.Add(
                    item.LastTournamentDate.HasValue ?
                        $"Inactive since {item.LastTournamentDate:yyyy-MM-dd}" :
                        "No tournament history"
                );
            }
            if (issueType.Equals("never-played", StringComparison.OrdinalIgnoreCase))
            {
                item.Issues.Add("Never played a tournament");
            }
        }

        // For invalid-* types, only keep items that actually have the invalid issue
        if (issueType.Equals("invalid-email", StringComparison.OrdinalIgnoreCase))
        {
            list = list.Where(x => x.Issues.Contains("Invalid email format")).ToList();
        }
        if (issueType.Equals("invalid-phone", StringComparison.OrdinalIgnoreCase))
        {
            list = list.Where(x => x.Issues.Contains("Invalid phone format")).ToList();
        }

        return new PlayersWithIssuesDto
        {
            Players = list,
            TotalCount = list.Count
        };
    }

    public Task<ValidationResultDto> ValidatePlayerDataAsync(ValidatePlayerDto request)
    {
        var result = new ValidationResultDto { IsValid = true };

        if (!string.IsNullOrWhiteSpace(request.Email) && !PlayerDataValidator.IsValidEmail(request.Email))
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationErrorDto
            {
                Field = nameof(request.Email),
                ErrorMessage = "Invalid email format",
                SuggestedFix = "Use a valid email like name@example.com"
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Phone) && !PlayerDataValidator.IsValidPhone(request.Phone))
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationErrorDto
            {
                Field = nameof(request.Phone),
                ErrorMessage = "Invalid phone format",
                SuggestedFix = "Use international format, e.g., +84901234567"
            });
        }

        if (request.SkillLevel.HasValue && !PlayerDataValidator.IsValidSkillLevel(request.SkillLevel.Value))
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationErrorDto
            {
                Field = nameof(request.SkillLevel),
                ErrorMessage = "Skill level must be between 1 and 10"
            });
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// EXPORT PLAYERS - Export danh sách players ra CSV (Excel fallback unsupported without external libs)
    /// </summary>
    public async Task<Response> ExportPlayersAsync(PlayerFilterDto filter, bool includeTournamentHistory, string format, CancellationToken ct)
    {
        try
        {
            // Build base query with same filters as GetPlayersAsync
            var query = _db.Players
                .AsNoTracking()
                .Include(p => p.User)
                .AsQueryable();

            // Reuse filters (subset for export performance)
            if (!string.IsNullOrWhiteSpace(filter.SearchName))
            {
                var searchTerm = filter.SearchName.ToLower();
                query = query.Where(p => p.FullName.ToLower().Contains(searchTerm));
            }
            if (!string.IsNullOrWhiteSpace(filter.SearchEmail))
            {
                var searchEmail = filter.SearchEmail.ToLower();
                query = query.Where(p => p.Email != null && p.Email.ToLower().Contains(searchEmail));
            }
            if (!string.IsNullOrWhiteSpace(filter.SearchPhone))
            {
                var searchPhone = filter.SearchPhone;
                query = query.Where(p => p.Phone != null && p.Phone.Contains(searchPhone));
            }
            if (!string.IsNullOrWhiteSpace(filter.Country))
            {
                query = query.Where(p => p.Country == filter.Country);
            }
            if (!string.IsNullOrWhiteSpace(filter.City))
            {
                query = query.Where(p => p.City == filter.City);
            }
            if (filter.HasLinkedAccount.HasValue)
            {
                query = filter.HasLinkedAccount.Value
                    ? query.Where(p => p.UserId != null)
                    : query.Where(p => p.UserId == null);
            }
            if (filter.CreatedFrom.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= filter.CreatedFrom.Value);
            }
            if (filter.CreatedTo.HasValue)
            {
                var toDate = filter.CreatedTo.Value.AddDays(1);
                query = query.Where(p => p.CreatedAt < toDate);
            }

            // Get data (no pagination)
            var players = await query
                .Include(p => p.TournamentPlayers)
                    .ThenInclude(tp => tp.Tournament)
                .ToListAsync(ct);

            // Determine format - we only support CSV without external deps
            var isCsv = string.IsNullOrWhiteSpace(format) || format.Equals("csv", StringComparison.OrdinalIgnoreCase);

            if (!isCsv)
            {
                // Fallback: if Excel requested, return CSV with proper filename indicating xlsx unsupported
                format = "csv";
            }

            var sb = new System.Text.StringBuilder();

            if (!includeTournamentHistory)
            {
                // Header
                sb.AppendLine("PlayerId,FullName,Nickname,Email,Phone,Country,City,SkillLevel,LinkedUserId,LinkedUserEmail,CreatedAt");

                foreach (var p in players)
                {
                    var linkedEmail = p.User?.Email ?? string.Empty;
                    sb.AppendLine(
                        $"\"{p.Id}\"," +
                        $"\"{p.FullName}\"," +
                        $"\"{p.Nickname}\"," +
                        $"\"{p.Email}\"," +
                        $"\"{p.Phone}\"," +
                        $"\"{p.Country}\"," +
                        $"\"{p.City}\"," +
                        $"{(p.SkillLevel?.ToString() ?? string.Empty)}," +
                        $"\"{p.UserId}\"," +
                        $"\"{linkedEmail}\"," +
                        $"{p.CreatedAt:yyyy-MM-dd HH:mm:ss}"
                    );
                }
            }
            else
            {
                // Header with tournament columns (flattened, pipe-separated)
                sb.AppendLine("PlayerId,FullName,Email,Phone,Country,City,SkillLevel,LinkedUserEmail,CreatedAt,TournamentsCount,LastTournamentDate,TournamentHistory");

                foreach (var p in players)
                {
                    var tournaments = p.TournamentPlayers
                        .Where(tp => tp.Tournament != null)
                        .OrderByDescending(tp => tp.Tournament!.StartUtc)
                        .Select(tp => new {
                            tp.TournamentId,
                            Name = tp.Tournament!.Name,
                            Date = tp.Tournament!.StartUtc
                        })
                        .ToList();

                    var history = string.Join(" | ", tournaments.Select(t => $"{t.TournamentId}:{t.Name}:{t.Date:yyyy-MM-dd}"));
                    var lastDate = tournaments.FirstOrDefault()?.Date;

                    sb.AppendLine(
                        $"\"{p.Id}\"," +
                        $"\"{p.FullName}\"," +
                        $"\"{p.Email}\"," +
                        $"\"{p.Phone}\"," +
                        $"\"{p.Country}\"," +
                        $"\"{p.City}\"," +
                        $"{(p.SkillLevel?.ToString() ?? string.Empty)}," +
                        $"\"{p.User?.Email}\"," +
                        $"{p.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                        $"{tournaments.Count}," +
                        $"{(lastDate.HasValue ? lastDate.Value.ToString("yyyy-MM-dd") : string.Empty)}," +
                        $"\"{history}\""
                    );
                }
            }

            var fileName = includeTournamentHistory
                ? $"players_export_with_tournaments_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv"
                : $"players_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            var export = new
            {
                fileName,
                contentType = "text/csv",
                content = sb.ToString(),
                totalRecords = players.Count,
                exportedAt = DateTime.UtcNow
            };

            return Response.Ok(export);
        }
        catch (Exception ex)
        {
            return Response.Error($"Error exporting players: {ex.Message}");
        }
    }
}

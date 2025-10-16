using Microsoft.EntityFrameworkCore;
using PoolMate.Api.Data;
using PoolMate.Api.Dtos.Tournament;
using PoolMate.Api.Models;

namespace PoolMate.Api.Services
{
    public class BracketService : IBracketService
    {
        private readonly ApplicationDbContext _db;
        private readonly Random _rng = new();

        public BracketService(ApplicationDbContext db) => _db = db;

        //Preview
        public async Task<BracketPreviewDto> PreviewAsync(int tournamentId, CancellationToken ct)
        {
            var t = await _db.Tournaments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == tournamentId, ct)
                ?? throw new KeyNotFoundException("Tournament not found");

            var players = await _db.TournamentPlayers
                .Where(x => x.TournamentId == tournamentId)
                .Select(x => new PlayerSeed { TpId = x.Id, Name = x.DisplayName, Seed = x.Seed })
                .ToListAsync(ct);

            if (players.Count == 0)
                throw new InvalidOperationException("No players to draw.");

            var stage1 = BuildStagePreview(
                stageNo: 1,
                type: t.BracketType,                 // Single/Double cho Stage 1
                ordering: t.Stage1Ordering,
                size: NextPowerOfTwo(Math.Max(players.Count, t.BracketSizeEstimate ?? players.Count)),
                players: players
            );

            StagePreviewDto? stage2 = null;
            if (t.IsMultiStage)
            {
                if (t.AdvanceToStage2Count is null || t.AdvanceToStage2Count <= 0)
                    throw new InvalidOperationException("AdvanceToStage2Count must be set for multi-stage.");

                stage2 = BuildStagePreview(
                    stageNo: 2,
                    type: BracketType.SingleElimination,
                    ordering: t.Stage2Ordering,
                    size: t.AdvanceToStage2Count.Value,
                    players: null // ở Preview stage 2 bạn có thể để trống, hoặc lấy từ hạng dự kiến
                );
            }

            return new BracketPreviewDto
            {
                TournamentId = tournamentId,
                IsMultiStage = t.IsMultiStage,
                Stage1 = stage1,
                Stage2 = stage2
            };
        }

        public async Task CreateAsync(int tournamentId, CancellationToken ct)
        {
            var t = await _db.Tournaments
                .Include(x => x.Stages)
                .FirstOrDefaultAsync(x => x.Id == tournamentId, ct)
                ?? throw new KeyNotFoundException("Tournament not found");

            // chặn tạo lại nếu đã có matches
            var anyMatches = await _db.Matches.AnyAsync(m => m.TournamentId == tournamentId, ct);
            if (anyMatches) throw new InvalidOperationException("Bracket already created.");

            var players = await _db.TournamentPlayers
                .Where(x => x.TournamentId == tournamentId)
                .Select(x => new PlayerSeed { TpId = x.Id, Name = x.DisplayName, Seed = x.Seed })
                .ToListAsync(ct);

            if (players.Count == 0)
                throw new InvalidOperationException("No players to draw.");

            // Tạo Stage 1
            var sizeStage1 = NextPowerOfTwo(Math.Max(players.Count, t.BracketSizeEstimate ?? players.Count));
            var s1 = new TournamentStage
            {
                TournamentId = t.Id,
                StageNo = 1,
                Type = t.BracketType,
                Status = StageStatus.NotStarted,
                Ordering = t.Stage1Ordering,
                AdvanceCount = t.IsMultiStage ? t.AdvanceToStage2Count : null
            };
            _db.TournamentStages.Add(s1);
            await _db.SaveChangesAsync(ct); // để có StageId

            var slots1 = MakeSlots(players, sizeStage1, t.Stage1Ordering);
            BuildAndPersistSingleOrDouble(
                tournamentId: t.Id,
                stage: s1,
                type: t.BracketType,
                slots: slots1,
                ct: ct);

            // Tạo Stage 2 (nếu multi)
            if (t.IsMultiStage)
            {
                var s2 = new TournamentStage
                {
                    TournamentId = t.Id,
                    StageNo = 2,
                    Type = BracketType.SingleElimination,
                    Status = StageStatus.NotStarted,
                    Ordering = t.Stage2Ordering,
                    AdvanceCount = null
                };
                _db.TournamentStages.Add(s2);
                await _db.SaveChangesAsync(ct);

                // Stage 2 chưa biết người (lúc Preview/ Create DP cũng chỉ vẽ khung).
                // Ở đây mình persist khung SE rỗng theo size = AdvanceToStage2Count.
                var emptySlots = Enumerable.Repeat<PlayerSeed?>(null, t.AdvanceToStage2Count!.Value).ToList();
                BuildAndPersistSingle(
                    tournamentId: t.Id,
                    stage: s2,
                    slots: emptySlots,
                    ct: ct);
            }
        }

        // ---------------- Core builders ----------------

        private StagePreviewDto BuildStagePreview(
            int stageNo, BracketType type, BracketOrdering ordering, int size, List<PlayerSeed>? players)
        {
            var slots = MakeSlots(players, size, ordering);
            return type == BracketType.SingleElimination
                ? PreviewSingle(stageNo, ordering, slots)
                : PreviewDouble(stageNo, ordering, slots);
        }

        private List<PlayerSeed?> MakeSlots(List<PlayerSeed>? players, int size, BracketOrdering ordering)
        {
            var slots = Enumerable.Repeat<PlayerSeed?>(null, size).ToList();
            if (players is null || players.Count == 0) return slots;

            if (ordering == BracketOrdering.Random)
            {
                // Random
                var shuffled = players.ToList();
                FisherYates(shuffled);

                for (int i = 0; i < Math.Min(size, shuffled.Count); i++)
                    slots[i] = shuffled[i];
                return slots;
            }

            // Seeded
            var seeded = players.Where(p => p.Seed.HasValue).OrderBy(p => p.Seed!.Value).ToList();
            var unseeded = players.Where(p => !p.Seed.HasValue).ToList();

            FisherYates(unseeded);

            var seedSlots = TennisSeedPositions(size);
            for (int i = 0; i < seeded.Count && i < size; i++)
                slots[seedSlots[i]] = seeded[i];

            int u = 0;
            for (int i = 0; i < size; i++)
                if (slots[i] is null)
                    slots[i] = (u < unseeded.Count) ? unseeded[u++] : null;

                return slots;
        }


        private StagePreviewDto PreviewSingle(int stageNo, BracketOrdering ordering, List<PlayerSeed?> slots)
        {
            var dto = new StagePreviewDto
            {
                StageNo = stageNo,
                Type = BracketType.SingleElimination,
                Ordering = ordering,
                BracketSize = slots.Count
            };

            int n = slots.Count;
            int matchesRound1 = n / 2;

            var r1 = new RoundPreviewDto { RoundNo = 1 };
            for (int i = 0; i < matchesRound1; i++)
            {
                var p1 = slots[2 * i];
                var p2 = slots[2 * i + 1];
                r1.Matches.Add(new MatchPreviewDto
                {
                    PositionInRound = i + 1,
                    P1Name = p1?.Name,
                    P1Seed = p1?.Seed,
                    P2Name = p2?.Name,
                    P2Seed = p2?.Seed
                });
            }
            dto.Rounds.Add(r1);

            int roundNo = 2;
            int m = matchesRound1 / 2;
            while (m > 0)
            {
                var r = new RoundPreviewDto { RoundNo = roundNo++ };
                for (int i = 0; i < m; i++)
                {
                    r.Matches.Add(new MatchPreviewDto
                    {
                        PositionInRound = i + 1,
                        P1Name = null,
                        P2Name = null
                    });
                }
                dto.Rounds.Add(r);
                m /= 2;
            }

            return dto;
        }


        private StagePreviewDto PreviewDouble(int stageNo, BracketOrdering ordering, List<PlayerSeed?> slots)
        {
            // Preview tối giản: hiển thị Winners bracket giống SE; Losers tạo “khung” rỗng
            var dto = PreviewSingle(stageNo, ordering, slots);
            dto.Type = BracketType.DoubleElimination; // gắn lại type

            // Bạn có thể bổ sung preview chi tiết Losers sau; hiện tại đủ cho bước “Create & Go to”
            return dto;
        }

        private void BuildAndPersistSingleOrDouble(int tournamentId, TournamentStage stage, BracketType type,
            List<PlayerSeed?> slots, CancellationToken ct)
        {
            if (type == BracketType.SingleElimination)
            {
                BuildAndPersistSingle(tournamentId, stage, slots, ct);
            }
            else
            {
                // Winners bracket = SE
                BuildAndPersistSingle(tournamentId, stage, slots, ct, bracket: BracketSide.Winners);

                // Losers bracket: tạo khung rỗng (chưa cần next mapping chi tiết ở giai đoạn Preview/Create)
                // Khi bạn triển khai vận hành, sẽ bổ sung mapping NextLoser/NextWinner theo chuẩn DE.
                // Ở đây để tối giản: bỏ qua sinh match Losers cho bước đầu (hoặc sinh rỗng các round).
            }
        }

        private void BuildAndPersistSingle(int tournamentId, TournamentStage stage,
            List<PlayerSeed?> slots, CancellationToken ct, BracketSide bracket = BracketSide.Knockout)
        {
            int n = slots.Count;
            int roundNo = 1;
            int matches = n / 2;
            int cursor = 0;

            var created = new List<Match>();
            while (matches > 0)
            {
                for (int pos = 1; pos <= matches; pos++)
                {
                    var p1 = slots[cursor++];
                    var p2 = slots[cursor++];

                    created.Add(new Match
                    {
                        TournamentId = tournamentId,
                        StageId = stage.Id,
                        Bracket = bracket,
                        RoundNo = roundNo,
                        PositionInRound = pos,
                        Player1TpId = p1?.TpId,
                        Player2TpId = p2?.TpId,
                        Status = MatchStatus.NotStarted
                    });
                }

                // tạo next pointer trong cùng bracket: match (roundNo,pos) -> (roundNo+1, ceil(pos/2))
                // sẽ set sau khi có Id
                roundNo++;
                matches /= 2;
            }
            _db.Matches.AddRange(created);
            _db.SaveChanges(); // lấy Id để set NextWinnerMatchId

            // set next pointers (W → W) cho SE/KO (hoặc Winners của DE)
            var grouped = created.GroupBy(m => m.RoundNo).OrderBy(g => g.Key).ToList();
            for (int r = 0; r < grouped.Count - 1; r++)
            {
                var cur = grouped[r].OrderBy(x => x.PositionInRound).ToList();
                var nxt = grouped[r + 1].OrderBy(x => x.PositionInRound).ToList();
                for (int i = 0; i < cur.Count; i++)
                {
                    var nextPos = (i / 2); // 0-based
                    cur[i].NextWinnerMatchId = nxt[nextPos].Id;
                }
            }
            _db.SaveChanges();
        }

        // -------------- helpers --------------

        private static int NextPowerOfTwo(int x)
        {
            if (x < 1) return 1;
            int p = 1;
            while (p < x) p <<= 1;
            return p;
        }

        private static void FisherYates<T>(IList<T> a)
        {
            var rng = Random.Shared;
            for (int i = a.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
        }

        private static int[] TennisSeedPositions(int n)
        {
            var positions = new List<int> { 0, n - 1 };
            int block = n;
            while (positions.Count < n)
        {
                block /= 2;
                var next = new List<int>(positions.Count * 2);
                foreach (var p in positions)
            {
                    next.Add(p);
                    next.Add(p + block);
                }
                positions = next;
            }
            return positions.ToArray();
        }


        private sealed record PlayerSeed
        {
            public int TpId { get; init; }
            public string Name { get; init; } = default!;
            public int? Seed { get; init; }
        }
    }
}

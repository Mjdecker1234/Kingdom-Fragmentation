using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace KingdomFragmentation.Helpers
{
    /// <summary>
    /// Banner generation based on a large curated pool of vanilla-safe presets.
    /// Presets are shuffled each campaign run so kingdoms and clans do not keep
    /// reusing the same order, but every assignment still comes from a
    /// predefined pool instead of ad-hoc random generation.
    /// </summary>
    public static class BannerGenerator
    {
        private static readonly object _syncRoot = new object();
        private static readonly HashSet<string> _usedCodes = new HashSet<string>();
        private static string[] _presetPool = Array.Empty<string>();
        private static Queue<string> _sessionPool = new Queue<string>();

        // Tracks which sigil IDs have already been given to a kingdom this run so
        // each kingdom gets a visually distinct heraldic symbol.
        private static readonly HashSet<int> _usedKingdomSigilIds = new HashSet<int>();
        private static Queue<int> _kingdomSigilQueue = new Queue<int>();

        private static readonly int[] BackgroundMeshIds =
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
            25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36
        };

        private static readonly int[] PreferredSigilIds =
        {
            100, 102, 105, 108, 110, 113, 115, 118, 120, 123, 125, 128,
            130, 133, 135, 138, 140, 143, 145, 148, 150, 153, 155, 158,
            200, 201, 203, 205, 206, 208, 209, 210, 212, 214, 215, 217,
            218, 220, 221, 223,
            300, 302, 305, 308, 310, 313, 315, 318, 320, 323, 325, 328,
            330, 333, 335, 338, 340, 343, 345, 347,
            400, 402, 405, 408, 410, 413, 415, 418, 420, 423, 425, 428,
            430, 433, 435, 438, 440, 443, 445, 448, 450, 453, 455, 458,
            500, 503, 505, 508, 510, 513, 515, 518, 520, 523, 525, 528,
            530, 533, 535
        };

        // Vanilla-safe color IDs from Native/ModuleData/banner_icons.xml.
        private static readonly int[] BackgroundColorIds =
        {
            0, 2, 4, 6, 8, 10, 12, 14,
            158, 159, 160, 161, 162, 163, 164, 165
        };
        private static readonly uint[] BackgroundColorValues =
        {
            0xFFB57A1Eu, 0xFF284E19u, 0xFF793191u, 0xFF382188u,
            0xFF591645u, 0xFF429081u, 0xFF224277u, 0xFF8D291Au,
            0xFF234116u, 0xFF26406Du, 0xFF7A4253u, 0xFF5A1310u,
            0xFF2F2A2Bu, 0xFF744C38u, 0xFF594012u, 0xFFBF5A25u
        };

        private static readonly int[] SigilColorIds =
        {
            1, 3, 5, 7, 9, 11, 13, 15,
            166, 167, 168, 169, 170, 171, 172, 173
        };
        private static readonly uint[] SigilColorValues =
        {
            0xFF4E1A13u, 0xFFB4F0F1u, 0xFFFCDE90u, 0xFFDEA940u,
            0xFFFFAD54u, 0xFFEFC990u, 0xFFCEDAE7u, 0xFFF7BF46u,
            0xFFFFCF83u, 0xFF85827Fu, 0xFFCE9697u, 0xFF95A9CCu,
            0xFF89A78Bu, 0xFFFFB53Eu, 0xFFFFFFFFu, 0xFFC7BEB7u
        };

        private static readonly int[] SigilSizes = { 430, 480, 530, 580 };
        private static readonly int[] SigilOffsetXs =
        {
            764, 740, 788, 764, 764, 748, 780, 732, 796
        };
        private static readonly int[] SigilOffsetYs =
        {
            764, 764, 764, 740, 788, 748, 780, 764, 764
        };
        private static readonly int[] SigilRotations = { 0, 10, 350, 20, 340 };

        /// <summary>
        /// Reset tracking for a new fragmentation run.
        /// </summary>
        public static void Reset()
        {
            lock (_syncRoot)
            {
                EnsurePresetPool();
                _usedCodes.Clear();
                _sessionPool = BuildShuffledSessionPool();
                _usedKingdomSigilIds.Clear();
                _kingdomSigilQueue = BuildShuffledSigilQueue();
            }
        }

        /// <summary>
        /// Returns a unique banner from the predefined pool.
        /// </summary>
        public static Banner GenerateUniqueBanner(string seedId)
        {
            lock (_syncRoot)
            {
                EnsurePresetPool();
                return GenerateUniqueBannerCore(seedId);
            }
        }

        /// <summary>
        /// Lock-free core — must only be called while <see cref="_syncRoot"/> is held.
        /// </summary>
        private static Banner GenerateUniqueBannerCore(string? seedId)
        {
            if (_sessionPool.Count == 0)
                _sessionPool = BuildShuffledSessionPool();

            int attempts = _sessionPool.Count;
            for (int i = 0; i < attempts; i++)
            {
                string code = _sessionPool.Dequeue();
                if (_usedCodes.Contains(code))
                    continue;

                var banner = TryCreateBanner(code);
                if (banner != null)
                {
                    _usedCodes.Add(code);
                    return banner;
                }
            }

            string emergencyCode = BuildEmergencyCode(seedId ?? "kf_emergency",
                _usedCodes.Count + 1);
            var emergencyBanner = TryCreateBanner(emergencyCode);
            if (emergencyBanner != null)
            {
                _usedCodes.Add(emergencyCode);
                return emergencyBanner;
            }

            LogHelper.Warn("BannerGenerator: all preset attempts failed, using engine random fallback.");
            return Banner.CreateRandomBanner();
        }

        /// <summary>
        /// Returns a banner whose sigil shape is unique for this run and whose
        /// background/sigil colors are taken directly from the kingdom's preset palette.
        /// This avoids the recolor step (which can silently fail) and guarantees that
        /// each kingdom has both a distinct symbol and the correct faction colors.
        /// </summary>
        public static Banner GenerateKingdomStyledBanner(
            string seedId,
            uint primaryColor,
            uint secondaryColor)
        {
            lock (_syncRoot)
            {
                EnsurePresetPool();

                int bgColorId   = FindClosestColorId(primaryColor,   BackgroundColorIds, BackgroundColorValues);
                int sigilColorId = FindClosestColorId(secondaryColor, SigilColorIds,      SigilColorValues);

                // Pick a sigil ID that hasn't been used by any kingdom this run.
                int sigilId = PickNextKingdomSigilId(seedId);

                int seed        = StableHash(seedId ?? "kf_kingdom");
                int bgMesh      = BackgroundMeshIds[PositiveMod(seed,      BackgroundMeshIds.Length)];
                int sigilSize   = SigilSizes        [PositiveMod(seed * 7,  SigilSizes.Length)];
                int offsetIdx   = PositiveMod(seed * 13, SigilOffsetXs.Length);
                int rotation    = SigilRotations    [PositiveMod(seed * 17, SigilRotations.Length)];
                int mirror      = PositiveMod(seed, 2);

                string code = BuildTemplateCode(
                    bgMesh, bgColorId, sigilId, sigilColorId,
                    sigilSize, SigilOffsetXs[offsetIdx], SigilOffsetYs[offsetIdx],
                    mirror, rotation);

                var banner = TryCreateBanner(code);
                if (banner != null)
                {
                    _usedCodes.Add(code);
                    return banner;
                }

                // Fallback: pull from the pool and attempt a recolor.
                // GenerateUniqueBannerCore is called directly (no lock re-entry).
                LogHelper.Debug("BannerGenerator: direct kingdom code invalid for '" + seedId
                    + "', falling back to pool+recolor.");
                var poolBanner  = GenerateUniqueBannerCore(seedId);
                var recolored   = TryRecolorBanner(poolBanner, primaryColor, secondaryColor);
                return recolored ?? poolBanner;
            }
        }

        private static void EnsurePresetPool()
        {
            if (_presetPool.Length > 0)
                return;

            try
            {
                _presetPool = BuildPresetPool();
                _sessionPool = BuildShuffledSessionPool();
                LogHelper.Info("BannerGenerator prepared " + _presetPool.Length
                    + " vanilla-safe preset banners.");
            }
            catch (Exception ex)
            {
                _presetPool = Array.Empty<string>();
                _sessionPool = new Queue<string>();
                LogHelper.Error("BannerGenerator init failed: "
                    + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static Banner? TryCreateBanner(string code)
        {
            try
            {
                return new Banner(code);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("BannerGenerator: invalid preset code rejected: " + ex.Message);
                return null;
            }
        }

        private static Banner? TryRecolorBanner(
            Banner banner,
            uint primaryColor,
            uint secondaryColor)
        {
            try
            {
                string bannerCode = banner?.BannerCode ?? string.Empty;
                if (string.IsNullOrWhiteSpace(bannerCode))
                    return null;

                int backgroundColorId = FindClosestColorId(
                    primaryColor,
                    BackgroundColorIds,
                    BackgroundColorValues);
                int sigilColorId = FindClosestColorId(
                    secondaryColor,
                    SigilColorIds,
                    SigilColorValues);

                string recoloredCode = RecolorSingleLayerBannerCode(
                    bannerCode,
                    backgroundColorId,
                    sigilColorId);

                return TryCreateBanner(recoloredCode);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("BannerGenerator: recolor failed: " + ex.Message);
                return null;
            }
        }

        private static string RecolorSingleLayerBannerCode(
            string bannerCode,
            int backgroundColorId,
            int sigilColorId)
        {
            string[] parts = bannerCode.Split('.');
            if (parts.Length < 20)
                return bannerCode;

            parts[1] = backgroundColorId.ToString();
            parts[2] = backgroundColorId.ToString();
            parts[11] = sigilColorId.ToString();
            parts[12] = sigilColorId.ToString();
            return string.Join(".", parts);
        }

        private static Queue<string> BuildShuffledSessionPool()
        {
            if (_presetPool.Length == 0)
                return new Queue<string>();

            var copy = new string[_presetPool.Length];
            Array.Copy(_presetPool, copy, _presetPool.Length);

            var rng = CreateRunRandom();
            for (int i = copy.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                string tmp = copy[i];
                copy[i] = copy[j];
                copy[j] = tmp;
            }

            return new Queue<string>(copy);
        }

        private static Queue<int> BuildShuffledSigilQueue()
        {
            var copy = new int[PreferredSigilIds.Length];
            Array.Copy(PreferredSigilIds, copy, PreferredSigilIds.Length);

            var rng = CreateRunRandom();
            for (int i = copy.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = copy[i];
                copy[i] = copy[j];
                copy[j] = tmp;
            }

            return new Queue<int>(copy);
        }

        /// <summary>
        /// Returns a sigil ID that has not yet been assigned to any kingdom this run.
        /// Falls back to a deterministic choice once all sigils are exhausted.
        /// </summary>
        private static int PickNextKingdomSigilId(string seedId)
        {
            // Replenish the queue if depleted (second pass recycles sigils).
            if (_kingdomSigilQueue.Count == 0)
                _kingdomSigilQueue = BuildShuffledSigilQueue();

            for (int attempts = PreferredSigilIds.Length; attempts > 0; attempts--)
            {
                if (_kingdomSigilQueue.Count == 0)
                    _kingdomSigilQueue = BuildShuffledSigilQueue();

                int id = _kingdomSigilQueue.Dequeue();
                if (!_usedKingdomSigilIds.Contains(id))
                {
                    _usedKingdomSigilIds.Add(id);
                    return id;
                }
            }

            // All sigils exhausted — deterministic overflow per seed.
            int seed = StableHash((seedId ?? "kf_overflow") + "|" + _usedKingdomSigilIds.Count);
            return PreferredSigilIds[PositiveMod(seed, PreferredSigilIds.Length)];
        }

        private static Random CreateRunRandom()
        {
            unchecked
            {
                int seed = Environment.TickCount;
                seed ^= (int)DateTime.UtcNow.Ticks;
                seed ^= Guid.NewGuid().GetHashCode();
                return new Random(seed);
            }
        }

        private static string[] BuildPresetPool()
        {
            var presets = new List<string>(PreferredSigilIds.Length * 4);
            var seenCodes = new HashSet<string>();

            for (int i = 0; i < PreferredSigilIds.Length; i++)
            {
                for (int variant = 0; variant < 4; variant++)
                {
                    int bgColorIndex = PositiveMod((i * 5) + (variant * 3), BackgroundColorIds.Length);
                    int sigilColorIndex = PositiveMod((i * 7) + (variant * 2) + 1, SigilColorIds.Length);

                    int bgMesh = BackgroundMeshIds[PositiveMod((i * 3) + (variant * 11), BackgroundMeshIds.Length)];
                    int sigilSize = SigilSizes[PositiveMod(i + variant, SigilSizes.Length)];
                    int offsetIndex = PositiveMod((i * 2) + (variant * 3), SigilOffsetXs.Length);
                    int rotation = SigilRotations[PositiveMod(i + variant, SigilRotations.Length)];
                    int mirror = ((i + variant) % 2 == 0) ? 0 : 1;

                    string code = BuildTemplateCode(
                        bgMesh,
                        BackgroundColorIds[bgColorIndex],
                        PreferredSigilIds[i],
                        SigilColorIds[sigilColorIndex],
                        sigilSize,
                        SigilOffsetXs[offsetIndex],
                        SigilOffsetYs[offsetIndex],
                        mirror,
                        rotation);

                    if (seenCodes.Add(code))
                        presets.Add(code);
                }
            }

            if (presets.Count == 0)
                presets.Add(BuildEmergencyCode("kf_bootstrap", 1));

            return presets.ToArray();
        }

        private static string BuildTemplateCode(
            int backgroundMesh,
            int backgroundColorId,
            int sigilId,
            int sigilColorId,
            int sigilSize,
            int sigilX,
            int sigilY,
            int sigilMirror,
            int sigilRotation)
        {
            return backgroundMesh + "." + backgroundColorId + "." + backgroundColorId
                 + ".4345.4345.764.764.1.0.0."
                 + sigilId + "." + sigilColorId + "." + sigilColorId
                 + "." + sigilSize + "." + sigilSize
                 + "." + sigilX + "." + sigilY
                 + ".0." + sigilMirror + "." + sigilRotation;
        }

        private static string BuildEmergencyCode(string seedId, int ordinal)
        {
            int seed = StableHash(seedId + "|" + ordinal);

            int bgMesh = BackgroundMeshIds[PositiveMod(seed * 3, BackgroundMeshIds.Length)];
            int sigil = PreferredSigilIds[PositiveMod(seed * 5, PreferredSigilIds.Length)];
            int bgColor = BackgroundColorIds[PositiveMod(seed * 7, BackgroundColorIds.Length)];
            int sigilColor = SigilColorIds[PositiveMod(seed * 11, SigilColorIds.Length)];
            int sigilSize = SigilSizes[PositiveMod(seed * 13, SigilSizes.Length)];
            int offsetIndex = PositiveMod(seed * 17, SigilOffsetXs.Length);
            int rotation = SigilRotations[PositiveMod(seed * 19, SigilRotations.Length)];
            int mirror = PositiveMod(seed, 2) == 0 ? 0 : 1;

            return BuildTemplateCode(
                bgMesh,
                bgColor,
                sigil,
                sigilColor,
                sigilSize,
                SigilOffsetXs[offsetIndex],
                SigilOffsetYs[offsetIndex],
                mirror,
                rotation);
        }

        private static int PositiveMod(int value, int mod)
        {
            int rem = value % mod;
            return rem < 0 ? rem + mod : rem;
        }

        private static int FindClosestColorId(
            uint targetColor,
            int[] colorIds,
            uint[] colorValues)
        {
            if (colorIds.Length == 0 || colorValues.Length == 0)
                return 0;

            int bestIndex = 0;
            long bestDistance = long.MaxValue;

            for (int i = 0; i < colorIds.Length && i < colorValues.Length; i++)
            {
                long distance = ColorDistanceSquared(targetColor, colorValues[i]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return colorIds[bestIndex];
        }

        private static long ColorDistanceSquared(uint a, uint b)
        {
            int ar = (int)((a >> 16) & 0xFF);
            int ag = (int)((a >> 8) & 0xFF);
            int ab = (int)(a & 0xFF);

            int br = (int)((b >> 16) & 0xFF);
            int bg = (int)((b >> 8) & 0xFF);
            int bb = (int)(b & 0xFF);

            int dr = ar - br;
            int dg = ag - bg;
            int db = ab - bb;
            return (dr * dr) + (dg * dg) + (db * db);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash * 31) + value[i];
                return hash;
            }
        }
    }
}

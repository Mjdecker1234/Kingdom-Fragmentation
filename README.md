# Kingdom Fragmentation

A **Mount &amp; Blade II: Bannerlord** mod that transforms the starting political map
into a free-for-all by splitting every eligible vanilla kingdom into many
independent kingdoms — one per clan — on new-campaign creation.

---

## Features

| Area | What the mod does |
|------|-------------------|
| **Fragmentation** | Every eligible clan becomes its own independent kingdom at campaign start |
| **Target Kingdom Count** | Set a specific number of kingdoms to generate — best clans become leaders, rest are distributed |
| **MCM settings** | Full Mod Configuration Menu with 6 categories and safe defaults |
| **Diplomacy** | Configurable starting state: all-peace, all-war, or rivalry-based |
| **Ownership** | Clan settlements are preserved under the new kingdom |
| **Clan Loyalty** | Active enforcement prevents clans from defecting back during lockout period |
| **Stability** | Starting treasury, influence, anti-collapse protection, and truce enforcement |
| **Compatibility** | Works in Sandbox and Story Mode; save/load safe; error-safe fallback |

---

## Requirements

| Dependency | Version |
|------------|---------|
| Mount &amp; Blade II: Bannerlord | e1.8.x or later (tested up to e2.x) |
| [Mod Configuration Menu (MCM)](https://www.nexusmods.com/mountandblade2bannerlord/mods/612) | v5.x |

---

## Installation

1. Download or build the mod (see [Building](#building)).
2. Copy the `KingdomFragmentation/` folder into your Bannerlord `Modules/` directory:
   ```
   …\Mount & Blade II Bannerlord\Modules\KingdomFragmentation\
   ```
3. Make sure **MCM** is installed and enabled in the launcher.
4. Enable **KingdomFragmentation** in the Bannerlord launcher mod list.
5. Start a **new** campaign — the mod runs automatically.

> **Important:** Changes only apply to *new* campaigns.  Loading an existing save
> will not re-run the fragmentation.

---

## Building

### Prerequisites

* [.NET SDK 4.7.2 or later](https://dotnet.microsoft.com/download)
* Bannerlord installed (used for DLL references)

### Build

```bash
cd KingdomFragmentation

# Point to your game installation (adjust path as needed)
dotnet build -p:GameFolder="C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
```

The compiled `KingdomFragmentation.dll` will appear in
`KingdomFragmentation\bin\Debug\net472\`.  Copy it and `SubModule.xml` into
`Modules\KingdomFragmentation\bin\Win64_Shipping_Client\`.

---

## How Fragmentation Works

1. **Trigger** — The mod registers a `CampaignBehaviorBase` that listens to
   `OnNewGameCreatedPartialFollowUpEnd`.  This event fires after all vanilla
   campaign setup (settlements, clans, heroes) has fully initialised, and
   *before* the player takes their first action.

2. **Eligibility filter** — `FragmentationEngine` iterates over every clan in
   every vanilla kingdom and applies the filters configured in MCM (tier, minor
   factions, mercenaries, etc.).

3. **Kingdom creation** — `KingdomCreator` calls
   `Kingdom.CreateKingdom(uniqueStringId)` to register the new faction,
   initialises it via `Kingdom.InitializeKingdom(...)`, then calls
   `ChangeKingdomAction.ApplyByJoinToKingdom(clan, newKingdom)` to move the
   clan.

4. **Settlement reassignment** — `SettlementReassigner` verifies that every
   town and castle owned by the clan reports the new kingdom as its map faction,
   fixing any edge cases with `ChangeOwnerOfSettlementAction`.

5. **Diplomacy** — `DiplomacyInitializer` applies the chosen starting state
   (all-peace / all-war / rivalry-based) across all new kingdoms.

6. **Guard flag** — `_fragmentationApplied` is serialised into the save file so
   that reloading never re-triggers the process.

---

## MCM Settings Reference

| Group | Setting | Default | Description |
|-------|---------|---------|-------------|
| Fragmentation Scope | Enable Fragmentation | on | Master switch |
| Fragmentation Scope | Major Noble Clans Only | on | Respects minimum tier |
| Fragmentation Scope | Include Ruling Clans | on | Splits original rulers too |
| Fragmentation Scope | Include Minor Factions | off | Can be unstable |
| Fragmentation Scope | Include Mercenaries | off | |
| Fragmentation Scope | Include Rebel Clans | off | |
| Fragmentation Scope | Minimum Clan Tier | 1 | |
| Kingdom Creation | Target Kingdom Count | 0 | 0 = one per clan; >0 = generate exactly N kingdoms |
| Kingdom Creation | One Clan = One Kingdom | on | Recommended mode |
| Kingdom Creation | Preserve Culture | on | |
| Kingdom Creation | Naming Mode | ClanBased | ClanBased / SettlementBased / CultureBased |
| Kingdom Creation | Name Prefix | "Kingdom of " | |
| Kingdom Creation | Banner Mode | KeepClan | KeepClan / Randomize / CultureBased |
| Diplomacy | Starting State | AllPeace | AllPeace / AllWar / RivalryBased |
| Diplomacy | Starting Truce (days) | 0 | |
| Diplomacy | Join Grace Period (days) | 30 | Enforced — clans forced to stay |
| Diplomacy | Defection Lockout (days) | 60 | Enforced — prevents reunification |
| Ownership | Preserve Settlement Ownership | on | |
| Ownership | Skip Landless Clans | off | |
| Stability | Starting Treasury | 50 000 | Gold per new kingdom leader |
| Stability | Starting Influence | 200 | |
| Stability | Anti-Collapse Protection (days) | 14 | Enforced — revives collapsed kingdoms |
| Compatibility | New Campaigns Only | on | |
| Compatibility | Debug Logging | off | |
| Compatibility | Dry-Run Mode | off | |
| Compatibility | Error-Safe Fallback | on | |

---

## Known Compatibility Notes

* **New campaigns only** — The `_fragmentationApplied` save flag and the
  `NewCampaignsOnly` MCM setting ensure the mod never fires on loaded saves.
* **MCM required** — MCM v5.x (`Bannerlord.MBOptionScreen`) is a **required**
  dependency declared in `SubModule.xml`.  The mod will not load without it.
  Install MCM v5.x before enabling KingdomFragmentation.
* **Active enforcement** — `DefectionLockoutDays`, `JoinGracePeriodDays`, and
  `AntiCollapseProtectionDays` are now actively enforced via a daily-tick handler
  that monitors clan assignments and forces defecting clans back to their assigned
  kingdoms.  `StartingTruceDays` is also enforced via daily tick.  The player's
  clan is always exempt from loyalty enforcement.
  `DisableDiplomacyDays` remains advisory-only and requires an optional Harmony
  patch for full AI interception.
* **Sandbox / story mode** — Both are supported; the trigger fires after the
  initial world setup in either mode.
* **Other mods** — Mods that also alter kingdom/clan structure at campaign start
  may conflict.  Load order matters; placing KingdomFragmentation last is safest.
* **Vanilla clans with no settlements** — Handled gracefully; by default such
  clans still become kingdoms (unless `Skip Landless Clans` is enabled).

---

## Licence

MIT

using System.Runtime.CompilerServices;

// Exposes internal test hooks (e.g. CampaignStats.NextBestCombo) to the EditMode test assembly
// without widening the public runtime API. Mirrors the same attribute in Player/Ship/Combat/Enemies.
[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]

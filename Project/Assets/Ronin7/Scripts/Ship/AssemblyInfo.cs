using System.Runtime.CompilerServices;

// Exposes internal test hooks (e.g. ShipController's smoothed steering rates, CockpitRecenter's
// yaw-flattening core) to the EditMode test assembly without widening the public runtime API.
[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]

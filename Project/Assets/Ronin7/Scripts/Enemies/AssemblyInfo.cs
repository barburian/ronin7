using System.Runtime.CompilerServices;

// Exposes internal test hooks (e.g. PostureMeter's pure Accumulate/Decay/IsBroken statics) to the
// EditMode test assembly without widening the public runtime API. Mirrors the identical
// AssemblyInfo.cs already present in Ronin7.Player/Ship/Combat/Audio/Flow.
[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]

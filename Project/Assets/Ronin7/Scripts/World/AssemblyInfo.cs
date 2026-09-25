using System.Runtime.CompilerServices;

// Exposes internal test hooks (e.g. RunArenaController.ReleaseCount, the A5.5 concurrency-cap pure
// seam) to the EditMode test assembly without widening the public runtime API. Mirrors the identical
// AssemblyInfo.cs already present in Ronin7.Core/Player/Ship/Combat/Audio/Enemies/Flow.
[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]

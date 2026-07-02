using System.Runtime.CompilerServices;

// Exposes internal test hooks on CombatVfxController (Subscribe/Unsubscribe/HandledCount) to the
// EditMode test assembly without widening the public runtime API.
[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]

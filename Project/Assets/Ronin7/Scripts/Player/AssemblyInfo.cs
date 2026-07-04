using System.Runtime.CompilerServices;

// Exposes internal test hooks (e.g. ZeroGGrabLocomotion.CircularBuffer<T>) to the EditMode test
// assembly without widening the public runtime API.
[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]

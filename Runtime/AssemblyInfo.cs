using System.Runtime.CompilerServices;

// The testing utilities drive gestures directly (GestureHarness), and the package tests check internals.
[assembly: InternalsVisibleTo("Threads.TouchController.Testing")]
[assembly: InternalsVisibleTo("Threads.TouchController.Tests.Gestures")]
[assembly: InternalsVisibleTo("Threads.TouchController.Tests.Intentions")]

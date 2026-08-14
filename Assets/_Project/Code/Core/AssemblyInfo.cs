using System.Runtime.CompilerServices;

// Mutating state such as "this piece is now a booster" is internal on purpose: only the core's own
// services may do it during turn resolution. Tests need the same access to build exact scenarios.
[assembly: InternalsVisibleTo("Project.Tests.EditMode")]
[assembly: InternalsVisibleTo("Project.Tests.PlayMode")]

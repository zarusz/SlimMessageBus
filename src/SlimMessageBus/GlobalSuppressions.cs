
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "This is a marker interface", Scope = "type", Target = "~T:SlimMessageBus.IRequestMessage`1")]
[assembly: SuppressMessage("Design", "CA1044:Properties should not be write only", Justification = "Intended", Scope = "member", Target = "~P:SlimMessageBus.IConsumerWithHeaders.Headers")]


// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This needs to be an array", Scope = "member", Target = "~P:SlimMessageBus.Host.MessageWithHeaders.Payload")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is intended.", Scope = "member", Target = "~M:SlimMessageBus.Host.MessageQueueWorker`1.WaitAll~System.Threading.Tasks.Task{SlimMessageBus.Host.MessageQueueResult{`0}}")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is intended.", Scope = "member", Target = "~M:SlimMessageBus.Host.Utils.DisposeSilently(System.IDisposable,System.Action{System.Exception})")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Intended", Scope = "member", Target = "~M:SlimMessageBus.Host.MessageBusBase.OnProducedHook(System.Object,System.String,SlimMessageBus.Host.Config.ProducerSettings)")]
[assembly: SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "<Pending>")]

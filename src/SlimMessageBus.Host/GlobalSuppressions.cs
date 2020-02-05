
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This needs to be an array", Scope = "member", Target = "~P:SlimMessageBus.Host.MessageWithHeaders.Payload")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is intended.", Scope = "member", Target = "~M:SlimMessageBus.Host.ConsumerInstancePool`1.ProcessMessage(`0)~System.Threading.Tasks.Task")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is intended.", Scope = "member", Target = "~M:SlimMessageBus.Host.MessageQueueWorker`1.WaitAll~System.Threading.Tasks.Task{SlimMessageBus.Host.MessageQueueResult{`0}}")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is intended.", Scope = "member", Target = "~M:SlimMessageBus.Host.ConsumerInstancePoolMessageProcessor`1.ProcessMessage(`0)~System.Threading.Tasks.Task")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is intended.", Scope = "member", Target = "~M:SlimMessageBus.Host.Utils.DisposeSilently(System.IDisposable,System.Action{System.Exception})")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This is intended.", Scope = "member", Target = "~M:SlimMessageBus.Host.MessageBusBase.OnResponseArrived(System.Byte[],System.String,System.String,System.String)~System.Threading.Tasks.Task")]

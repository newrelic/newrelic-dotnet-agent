# CallContext Logical Data (aka “AsyncLocal”)

* .NET 4.5+
* Copy on write from “true async” methods due to async ExecutionContext behavior
* From sync method, stays on thread for life of thread in thread pool
* Global / not instance based
* Will hop application domains and follow remoting calls 

Data is tracked via logical call context data in pattern dubbed “AsyncLocal” before `AsyncLocal` was created officially by Microsoft. Logical call context data will flow with the `ExecutionContext` across async and thread boundaries. 

Writing data from a “true async” method creates a new copy of the `ExecutionContext` which will only flow to children of that method. There are multiple benefits to this but one key one is that you do not need to worry about the data remaining on the original thread when this happens. The original `ExecutionContext` is not modified.

`CallContext` logical data storage was introduced for .NET remoting and does come with some drawbacks for our usage. Data stored in the logical call context data has to be serializable or risk throwing exceptions in certain types of applications. This data will be marshalled across remoting calls or application domains. Objects that are stored as [MarshalByRefObj types will have their reference passed](https://docs.microsoft.com/en-us/dotnet/standard/serialization/serialization-concepts#marshal-by-value). Objects that are simply serializable will result in a copy by value. We don’t support these use cases but want to avoid throwing serialization exceptions. *Note: Marshalling the reference can result in delayed serialization exceptions during the execution of code on that reference, particularly anything involving an enumerator.*

This is tricky for us trying not to crash in the wild even though we don’t “support” these cases. Currently, we are wrapping data in a serializable container that won’t actually serialize the underlying data. We hope to avoid crashes but don’t intend to support cross-app domain calls or remoting.

For more on the origins of this storage, see: [Implicit Async Context ("AsyncLocal")](https://blog.stephencleary.com/2013/04/implicit-async-context-asynclocal.html)

## Segment Parenting / "Call Stack"

At the start of a transaction, we clear the state. Since our entry point may be a synchronous method and we may end on a separate thread, the starting thread may retain the previous transaction’s parenting data for that method’s context. We then take advantage of the “copy on write” `ExecutionContext` behavior from async methods to appropriately track parenting. Writing data from an async method creates a new copy of the `ExecutionContext`, of which data will only flow to children of that method. This means asynchronously executing methods will not be reading/writing to the same parent data and will not accidently nest under each-other. In essence, the data forks with the call tree in async methods.



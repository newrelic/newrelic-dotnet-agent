# v8.24
## Public Facing Notes

### New Features
* **Adding Custom Transaction Attributes using the .NET Agent API**<br/>
  New method, `AddCustomAttribute(string,object)` has been added to `ITransaction`.
  <br/>  
  * This new method accepts and supports all data-types.
  * Method `AddCustomParameter(string,IConvertable)` is still available with limited data-type support; however, this method should be considered obsolete and will  be removed in a future release of the Agent API.
  * Further information may be found within [.NET Agent API documentation](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api/itransaction-0).
<br/>

* **Enhanced type support for `RecordCustomEvent` and `NoticeError` API Methods.**<br/>
APIs for recording exceptions and custom events now support values of all types.
  <br/> 
  * The `NoticeError` API Method has new overloads that accept an `IDictionary<string,object>`.
  * The `RecordCustomEvent` methods has been modified to handle all types of data.  In that past, they only handled `string` and `float` types.
  * Further information may be found within [.NET Agent API documentation](https://docs.newrelic.com/docs/agents/net-agent/net-agent-api).
<br/>


### Bug Fixes
*
### Other Changes
* **New Relic Identifier Format Changes**<br/> 
  Starting with v8.24, the format of identifiers for `TransactionId` and `SpanId` will only contain lowercase characters. Prior versions of the agent produce identifiers with uppercase characters.  As a result, older versions of the agent may not propagate W3C headers, resulting in broken traces. 


## Internal Facing Notes

____

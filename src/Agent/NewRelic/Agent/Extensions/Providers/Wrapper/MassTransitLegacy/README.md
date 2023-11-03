This is the wrapper and instrumentation project for version 7.x of [MassTransit](https://masstransit.io/).

The code in this project is nearly identical to that in the `MassTransit` project, which is for MassTransit v8. However, different MassTransit types are referenced
in the two projects. The team decided at the time this instrumentation was introduced that eliminating the duplicate code would make things far less readable,
and that code duplication was the lesser of two evils.

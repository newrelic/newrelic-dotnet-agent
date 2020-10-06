## Integration Test Application Helper Libraries ##
Projects are small units that can be brought into Integration Test Applications to perform units of work that are tested.  They help us not copy the same code across multiple applications and make it a bit easier to implement the same test across .NET Core and .NET Framework.

* They should _Always_ be NET STANDARD so that they can be shared across both framework and core projects.
* They should be small so that they can be brought into test projects without introducing any extra content.


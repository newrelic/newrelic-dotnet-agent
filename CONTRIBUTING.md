# Contributing

Contributions are always welcome. Before contributing please read the
[code of conduct](https://opensource.newrelic.com/code-of-conduct/) and [search the issue tracker](https://github.com/newrelic/newrelic-dotnet-agent/issues); your issue may have already been discussed or fixed in `master`. To contribute,
[fork](https://docs.github.com/en/get-started/quickstart/fork-a-repo) this repository, commit your changes, and [send a Pull Request](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/about-pull-requests).

Note that our [code of conduct](https://opensource.newrelic.com/code-of-conduct/) applies to all platforms and venues related to this project; please follow it in all yo ur interactions with the project and its participants.

## Feature Requests

Feature requests should be submitted in the [Issue tracker](https://github.com/newrelic/newrelic-dotnet-agent/issues), with a description of the expected behavior & use case, where they’ll remain closed until sufficient interest, [e.g. :+1: reactions](https://docs.github.com/en/get-started/quickstart/communicating-on-github), has been [shown by the community](https://github.com/newrelic/newrelic-dotnet-agent/issues?q=label%3A%22votes+needed%22+sort%3Areactions-%2B1-desc).
Before submitting an Issue, please search for similar ones in the
[closed issues](https://github.com/newrelic/newrelic-dotnet-agent/issues?q=is%3Aissue+is%3Aclosed+label%3Aenhancement).

## Pull Requests

### Version Support

When contributing, please keep in mind that New Relic customers (that's you!) are running many different versions of .NET, some of them pretty old. Changes that depend on the newest version of .NET Framework or Core will probably be rejected, especially if they replace something backwards compatible.  Code in `src/Agent` or `src/NewRelic.Core` needs to be compatible with .NET Framework 4.5+ and with .NET Core 2.0+.  Backwards compatibility is less important for code that lives in `tests`, but still should not gratuitously require features only available in the latest .NET Framework or Core.

Be aware that the instrumentation needs to work with a wide range of versions of the instrumented modules, and that code that looks nonsensical or overcomplicated may be that way for compatibility-related reasons. Read all the comments and check the related tests before deciding whether existing code is incorrect.

If you’re planning on contributing a new feature or an otherwise complex contribution, we kindly ask you to start a conversation with the maintainer team by opening up a GitHub issue first.

### General Guidelines

A primary goal of the agent is to adhere to the Hippocratic oath for the applications it instruments: “first, do no harm”.  The code in the agent will execute inside the process space of our customers’ applications and must not crash them, destabilize them, change their behavior, or significantly degrade their performance.  Another important consideration is that the data the agent gathers must not leak any personally identifying information (PII) from customer apps, e.g. usernames or credit card numbers.

This project is licensed under the Apache-2.0 license.  Any third party libraries added as dependencies of the project must have a similarly permissive open source license, e.g. MIT.

### Coding Style Guidelines

Our repository includes an [.editorconfig](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/.editorconfig) which will be used automatically by Visual Studio to maintain consistent code formatting.

#### Variable naming conventions

| Format      | Use For | Example |
| ----------- | ------- | ------- |
| PascalCase  | Constant fields, public class level fields | `private const string MyConst = "NewRelic";` |
| camelCase  | Local variables (note: we prefer the use of the `var` implicit definition keyword whenever possible) | `var myId = 12345;` |
| _camelcase  | Private class-level fields, including static and/or read-only | `private int _myId;` |

#### Class naming conventions

- All class names should be written in PascalCase and should be singular, e.g. `TransactionName`.  If it is a collection, it should be pluralized e.g. `TransactionNames`.  Code files can contain multiple classes if the classes are tightly coupled, but this should be rare.
- All class declarations should have access modifiers.

#### Interface naming conventions

- All interfaces names should be written in PascalCase and prefixed with the letter "I", e.g. `ITransactionName`.

#### Method naming conventions

- Methods should be named using PascalCase and parameters should be named using camelCase, e.g `private string GetUserNameFromId(int userId)`.

#### Class file layout

- The preferred order of declarations within a class is:
  - Fields
  - Properties
  - Methods (with constructors grouped at the beginning)
  - Events

### Testing Guidelines

See our [development](docs/development.md) and [integration testing](docs/integration-tests.md) documentation to run tests, including required setup steps.

For most contributions it is strongly recommended to add additional tests which exercise your changes. This helps us efficiently incorporate your changes into our mainline codebase and provides a safeguard that your change won't be broken by future development. Because of this, we require that all changes come with tests. You are welcome to submit pull requests with untested changes, but they won't be merged until you or the development team have an opportunity to write tests for them.

There are some rare cases where code changes do not result in changed functionality (e.g. a performance optimization) and new tests are not required. In general, including tests with your pull request dramatically increases the chances it will be accepted.

Integration tests are used to test the functionality of [agent instrumentation](src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper).  PRs that add or modify instrumentation should include new or updated integration tests.

## Contributor License Agreement

Keep in mind that when you submit your Pull Request, you'll need to sign the CLA via the click-through using CLA-Assistant. If you'd like to execute our corporate CLA, or if you have any questions, please drop us an email at opensource@newrelic.com.

For more information about CLAs, please check out Alex Russell’s excellent post,
[“Why Do I Need to Sign This?”](https://infrequently.org/2008/06/why-do-i-need-to-sign-this/).

using System;

namespace CompatibilityDocs.Schema;

public class SchemaValidationException : Exception
{
    public SchemaValidationException(string message) : base(message) { }
}

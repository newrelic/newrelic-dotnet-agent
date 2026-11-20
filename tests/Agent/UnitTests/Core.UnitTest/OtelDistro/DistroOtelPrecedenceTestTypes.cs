// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.OtelDistro.PrecedenceTestTypes;

// Hierarchy fixtures for DistroOtelPrecedenceDiagnosticsTests: an OTel Derived/Interface site names the base class or
// interface, the NR site names the concrete type.

public interface IPrecedenceTarget
{
    int Run(string value);
}

public abstract class PrecedenceBase
{
    public abstract int Run(string value);
}

public class PrecedenceDerived : PrecedenceBase
{
    public override int Run(string value) => 0;

    public int Walk() => 0;
}

public class PrecedenceGrandchild : PrecedenceDerived
{
    public override int Run(string value) => 1;
}

public class PrecedenceImplementation : IPrecedenceTarget
{
    public int Run(string value) => 0;
}

public class PrecedenceBoth : PrecedenceBase, IPrecedenceTarget
{
    public override int Run(string value) => 0;
}

// Implements an interface that is not an OTel target, so the interface walk runs and finds nothing.
public class PrecedenceUnrelated : IComparable
{
    public int Run(string value) => 0;

    public int CompareTo(object obj) => 0;
}

// Implements IPrecedenceTarget only through its base class (no InterfaceImpl row of its own).
public class PrecedenceInheritsInterface : PrecedenceImplementation
{
    public new int Run(string value) => 1;
}

// Parameter shapes as OTel's InstrumentMethodAttribute spells them: by-ref, class and method generic parameters, a
// generic instantiation and a rank-2 array.
public class PrecedenceShapes<T> : PrecedenceBase
{
    public override int Run(string value) => 0;

    public void Shape<U>(ref int count, T[] items, List<T> list, U extra, int[,] grid)
    {
    }
}

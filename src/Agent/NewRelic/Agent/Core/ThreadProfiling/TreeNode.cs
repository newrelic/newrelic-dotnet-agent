// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.ThreadProfiling;

public class BucketProfile
{
    public readonly ProfileNode Root = new ProfileNode(
        // Set the id to IntPtr.Zero so that we don't request it from the unmanaged profiler - 
        // such a request makes it unhappy.
        UIntPtr.Zero,
        0, 0);
}

public class ProfileNodes : IEnumerable<ProfileNode>
{
    private readonly List<ProfileNode> _children;

    public ProfileNodes()
    {
        _children = new List<ProfileNode>();
    }

    public int Count { get { return _children.Count; } }

    public void Add(ProfileNode node)
    {
        _children.Add(node);
    }

    public void Remove(ProfileNode node)
    {
        _children.Remove(node);
    }

    public void Clear()
    {
        if (_children.Count > 0)
        {
            _children.Clear();
        }
    }

    public object ToJsonObject()
    {
        return _children;
    }

    public IEnumerator<ProfileNode> GetEnumerator()
    {
        return _children.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return _children.GetEnumerator();
    }
}

/// <summary>
/// Represents a node in thread profile tree.
/// </summary>
[JsonConverter(typeof(JsonArrayConverter))]
public class ProfileNode
{
    [JsonArrayIndex(Index = 0)]
    public readonly ProfileNodeDetails Details = new ProfileNodeDetails();

    [JsonArrayIndex(Index = 1)]
    public uint RunnableCount;

    [JsonArrayIndex(Index = 2)]
    public readonly uint Unused;

    [JsonArrayIndex(Index = 3)]
    public readonly ProfileNodes Children = new ProfileNodes();

    public uint NonRunnableCount;

    public UIntPtr FunctionId { get; set; }
    public uint Depth { get; set; }
    public bool IgnoreForReporting { get; set; }

    public ProfileNode(UIntPtr functionId, uint runnableCount, uint depth)
    {
        FunctionId = functionId;
        RunnableCount = runnableCount;
        Depth = depth;
    }

    public void AddChild(ProfileNode node)
    {
        Children.Add(node);
    }

    public void ClearChildren()
    {
        Children.Clear();
    }
}

[JsonConverter(typeof(JsonArrayConverter))]
public class ProfileNodeDetails
{
    [JsonArrayIndex(Index = 0)]
    public string ClassName { get; set; }
    [JsonArrayIndex(Index = 1)]
    public string MethodName { get; set; }
    [JsonArrayIndex(Index = 2)]
    public uint LineNumber { get; set; }
}
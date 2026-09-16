// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;

namespace RuntimeAsyncTestApp.Controllers;

/// <summary>
/// A liveness endpoint, nothing more. The runtime-async work this application exists to exercise
/// runs at startup (see Program.Main), so the harness only needs something to connect to in order
/// to confirm the application came up.
/// </summary>
[ApiController]
[Route("[controller]")]
public class StatusController : ControllerBase
{
    [HttpGet]
    public string Get()
    {
        return "ok";
    }
}

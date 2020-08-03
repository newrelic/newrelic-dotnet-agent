/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreMvcBasicRequestsApplication.Controllers
{
    public class ExpectedErrorTestController : Controller
    {
        public void ThrowExceptionWithMessage(string exceptionMessage)
        {
            throw new Exception(exceptionMessage);
        }

        public void ThrowCustomException()
        {
            throw new CustomExceptionClass("This in a CustomExceptionClass exception.");
        }

        public void ReturnADesiredStatusCode(int statusCode)
        {
            Request.HttpContext.Response.StatusCode = statusCode;
        }
    }

    public class CustomExceptionClass : Exception
    {
        public CustomExceptionClass(string message) : base(message)
        {
        }
    }
}

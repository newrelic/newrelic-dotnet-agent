// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Web.Http;
using OwinRemotingShared;

namespace OwinRemotingClient.Controllers
{
    [RoutePrefix("Remote")]
    public class RemoteController : ApiController
    {
        [Route("GetObjectTcp")]
        public string GetObjectTcp()
        {
            var myMarshalByRefClassObj = (MyMarshalByRefClass)Activator.GetObject(typeof(MyMarshalByRefClass), "tcp://127.0.0.1:7878/GetObject");
            return GetObject(myMarshalByRefClassObj);
        }

        [Route("GetObjectHttp")]
        public string GetObjectHttp()
        {
            var myMarshalByRefClassObj = (MyMarshalByRefClass)Activator.GetObject(typeof(MyMarshalByRefClass), "http://127.0.0.1:7879/GetObject");
            return GetObject(myMarshalByRefClassObj);
        }

        private string GetObject(MyMarshalByRefClass myMarshalByRefClassObj)
        {
            var result = "No exception";

            try
            {
                var myReturnValue = myMarshalByRefClassObj.MyMethod();
            }
            catch (Exception ex)
            {
                result = ex.ToString();
            }

            return result;
        }
    }
}


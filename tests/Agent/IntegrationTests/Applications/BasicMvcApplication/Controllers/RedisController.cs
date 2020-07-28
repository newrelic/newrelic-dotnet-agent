using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using ServiceStack.Redis;

namespace BasicMvcApplication.Controllers
{
    public class RedisController : Controller
    {
        public string Get()
        {
            using (var client = new RedisClient("localhost"))
            {
                client.ServerVersionNumber = 1;

                ThisIsBadAndYouShouldFeelBad.SwallowExceptionsFromInvalidRedisHost(() => client.SaveAsync());
                ThisIsBadAndYouShouldFeelBad.SwallowExceptionsFromInvalidRedisHost(() => client.Shutdown());
                ThisIsBadAndYouShouldFeelBad.SwallowExceptionsFromInvalidRedisHost(() => client.RewriteAppendOnlyFileAsync());
            }

            return "Great success";
        }

        public class ThisIsBadAndYouShouldFeelBad
        {
            public static void SwallowExceptionsFromInvalidRedisHost(Action command)
            {
                try
                {
                    command();
                }
                catch
                {
                    //For reals, we should test against a real redis instance instead of 
                    //throwing exceptions every call.
                }
            }
        }
    }
}

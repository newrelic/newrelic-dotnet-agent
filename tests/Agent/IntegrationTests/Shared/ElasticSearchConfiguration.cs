// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class ElasticSearchConfiguration
    {
        private static string _elasticServer;
        private static string _elasticUserName;
        private static string _elasticPassword;

        public static string ElasticServer
        {
            get
            {
                if (_elasticServer == null)
                {
                    try
                    {
                        var testConfiguration =
                            IntegrationTestConfiguration.GetIntegrationTestConfiguration("ElasticSearchTests");
                        _elasticServer = testConfiguration["Server"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("ElasticServer configuration is invalid.", ex);
                    }
                }

                return _elasticServer;
            }
        }

        public static string ElasticUserName
        {
            get
            {
                if (_elasticUserName == null)
                {
                    try
                    {
                        var testConfiguration =
                            IntegrationTestConfiguration.GetIntegrationTestConfiguration("ElasticSearchTests");
                        _elasticUserName = testConfiguration["UserName"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("ElasticServer configuration is invalid.", ex);
                    }
                }
                return _elasticUserName;
            }
        }
        public static string ElasticPassword
        {
            get
            {
                if (_elasticPassword == null)
                {
                    try
                    {
                        var testConfiguration =
                            IntegrationTestConfiguration.GetIntegrationTestConfiguration("ElasticSearchTests");
                        _elasticPassword = testConfiguration["Password"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("ElasticServer configuration is invalid.", ex);
                    }
                }
                return _elasticPassword;
            }
        }
    }
    public class ElasticSearch7Configuration
    {
        private static string _elasticServer;
        private static string _elasticUserName;
        private static string _elasticPassword;

        public static string ElasticServer
        {
            get
            {
                if (_elasticServer == null)
                {
                    try
                    {
                        var testConfiguration =
                            IntegrationTestConfiguration.GetIntegrationTestConfiguration("ElasticSearch7Tests");
                        _elasticServer = testConfiguration["Server"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("ElasticServer configuration is invalid.", ex);
                    }
                }

                return _elasticServer;
            }
        }

        public static string ElasticUserName
        {
            get
            {
                if (_elasticUserName == null)
                {
                    try
                    {
                        var testConfiguration =
                            IntegrationTestConfiguration.GetIntegrationTestConfiguration("ElasticSearch7Tests");
                        _elasticUserName = testConfiguration["UserName"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("ElasticServer configuration is invalid.", ex);
                    }
                }
                return _elasticUserName;
            }
        }
        public static string ElasticPassword
        {
            get
            {
                if (_elasticPassword == null)
                {
                    try
                    {
                        var testConfiguration =
                            IntegrationTestConfiguration.GetIntegrationTestConfiguration("ElasticSearch7Tests");
                        _elasticPassword = testConfiguration["Password"];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("ElasticServer configuration is invalid.", ex);
                    }
                }
                return _elasticPassword;
            }
        }
    }
}

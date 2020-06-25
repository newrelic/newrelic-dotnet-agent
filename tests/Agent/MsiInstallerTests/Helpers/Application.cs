/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace FunctionalTests.Helpers
{
    public class Application
    {
        private String _name;
        public String Name { get { return _name; } set { _name = value; } }

        private String _baseUrlFormatter;
        public String BaseUrlFormatter { get { return _baseUrlFormatter; } set { _baseUrlFormatter = value; } }
    }
}

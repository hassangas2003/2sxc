﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ToSic.SexyContent.Environment.Interfaces;

namespace ToSic.SexyContent.Environment
{
    public class Environment: IEnvironment
    {
        public IPermissions Permissions { get; internal set; }
    }
}
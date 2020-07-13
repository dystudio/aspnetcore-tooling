﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    [Serial, Method(LanguageServerConstants.RazorMapToDocumentEditsEndpoint)]
    internal interface IRazorMapToDocumentEditsHandler : IJsonRpcRequestHandler<RazorMapToDocumentEditsParams, RazorMapToDocumentEditsResponse>
    {
    }
}

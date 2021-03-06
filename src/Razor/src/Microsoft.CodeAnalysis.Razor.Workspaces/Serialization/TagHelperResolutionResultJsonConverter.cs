﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Razor.Serialization
{
    internal class TagHelperResolutionResultJsonConverter : JsonConverter
    {
        private readonly JsonSerializer _serializer;
        public static readonly TagHelperResolutionResultJsonConverter Instance = new TagHelperResolutionResultJsonConverter();

        public TagHelperResolutionResultJsonConverter()
        {
            _serializer = new JsonSerializer();
            _serializer.Converters.Add(TagHelperDescriptorJsonConverter.Instance);
            _serializer.Converters.Add(RazorDiagnosticJsonConverter.Instance);
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(TagHelperResolutionResult);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Verify expected object structure based on `WriteJson`
            if (reader.TokenType != JsonToken.StartObject)
            {
                return null;
            }

            var descriptors = reader.ReadPropertyArray<TagHelperDescriptor>(_serializer, nameof(TagHelperResolutionResult.Descriptors));
            var diagnostics = reader.ReadPropertyArray<RazorDiagnostic>(_serializer, nameof(TagHelperResolutionResult.Diagnostics));

            reader.ReadTokenAndAdvance(JsonToken.EndObject, out _);

            return new TagHelperResolutionResult(descriptors, diagnostics);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var result = (TagHelperResolutionResult)value;

            writer.WriteStartObject();

            writer.WritePropertyArray(nameof(TagHelperResolutionResult.Descriptors), result.Descriptors, _serializer);
            writer.WritePropertyArray(nameof(TagHelperResolutionResult.Diagnostics), result.Diagnostics, _serializer);

            writer.WriteEndObject();
        }
    }
}

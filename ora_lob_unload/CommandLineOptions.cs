﻿namespace SK.NoP77svk.OraLobUnload
{
    using System;
    using System.Text;
    using CommandLine;

    internal class CommandLineOptions
    {
        [Option('f', "file", Required = false, HelpText = "Input SQL script file")]
        public string? InputSqlScriptFile { get; set; }

        [Option('o', "output", Required = false, HelpText = "Output folder")]
        public string? OutputFolder { get; set; }

        [Option('x', "output-file-extension", Required = false)]
        public string? OutputFileExtension { get; set; }

        [Option("clob-output-charset", Required = false, Default = "utf-8")]
        public string? OutputEncodingId { get; set; }

        [Option("file-name-column-ix", Required = false, Default = 1)]
        public int FileNameColumnIndex { get; set; }

        [Option("lob-column-ix", Required = false, Default = 2)]
        public int LobColumnIndex { get; set; }

        [Option("lob-init-fetch-size", Required = false, Default = "64K")]
        public string? LobFetchSize { get; set; }

        [Option('t', "use-table", Required = true, Default = false, SetName = "in-type-table")]
        public bool InputSqlReturnTypeTable { get; set; }

        [Option('q', "use-query", Required = true, Default = false, SetName = "in-type-query")]
        public bool InputSqlReturnTypeSelect { get; set; }

        [Option('c', "use-cursor", Required = true, Default = false, SetName = "in-type-cursor")]
        public bool InputSqlReturnTypeCursor { get; set; }

        [Option('m', "use-implicit-cursor", Required = true, Default = false, SetName = "in-type-implicit")]
        public bool InputSqlReturnTypeMultiImplicit { get; set; }

        [Option('u', "db-user", Required = true)]
        public string? DbUser { get; set; }

        [Option('p', "db-pasword", Required = true)]
        public string? DbPassword { get; set; }

        [Option('d', "db", Required = true)]
        public string? DbService { get; set; }

        internal Encoding OutputEncoding => OutputEncodingId switch
        {
            null or "" => new UTF8Encoding(false, false),
            _ => Encoding.GetEncoding(OutputEncodingId)
        };

        internal int LobFetchSizeB
        {
            get
            {
                if (LobFetchSize is null or "")
                {
                    return 262144;
                }
                else
                {
                    string lobFetchWoUnit = LobFetchSize[0..^1];
                    if (LobFetchSize.EndsWith("K", StringComparison.OrdinalIgnoreCase))
                        return Convert.ToInt32(lobFetchWoUnit) * 1024;
                    else if (LobFetchSize.EndsWith("M", StringComparison.OrdinalIgnoreCase))
                        return Convert.ToInt32(lobFetchWoUnit) * 1024 * 1024;
                    else if (LobFetchSize.EndsWith("G", StringComparison.OrdinalIgnoreCase))
                        return Convert.ToInt32(lobFetchWoUnit) * 1024 * 1024 * 1024;
                    else
                        throw new ArgumentOutOfRangeException(nameof(LobFetchSize), $"Unrecognized unit of LOB fetch size \"{LobFetchSize}\"");
                }
            }
        }

        internal InputSqlReturnType GetUltimateScriptType()
        {
            InputSqlReturnType result;
            if (InputSqlReturnTypeTable)
                result = InputSqlReturnType.Table;
            else if (InputSqlReturnTypeSelect)
                result = InputSqlReturnType.Select;
            else if (InputSqlReturnTypeCursor)
                result = InputSqlReturnType.RefCursor;
            else if (InputSqlReturnTypeMultiImplicit)
                result = InputSqlReturnType.MultiImplicitCursors;
            else
                throw new ArgumentOutOfRangeException("No input SQL return type specified");

            return result;
        }
    }
}
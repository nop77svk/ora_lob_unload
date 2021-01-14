﻿namespace OraLobUnload
{
    using System;
    using System.Data.Common;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using CommandLine;
    using Oracle.ManagedDataAccess.Client;

    internal static class Program
    {
        #pragma warning disable SA1500 // Braces for multi-line statements should not share line
        internal static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<CommandLineOptions>(args)
                .MapResult<CommandLineOptions, int>(
                    options => MainWithOptions(options),
                    _ => {
                        Console.WriteLine("Something bad happened on the command line (2do!)");
                        return 255;
                    }
                );
        }
        #pragma warning restore SA1500 // Braces for multi-line statements should not share line

        internal static int MainWithOptions(CommandLineOptions options)
        {
            var scriptType = InputSqlReturnTypeEnumHelpers.GetUltimateScriptType(options);

            using var inputSqlScriptReader = OpenInputSqlScript(options.InputSqlScriptFile);
            var inputSqlText = inputSqlScriptReader.ReadToEnd();

            using var dbConnection = new OracleConnection($"Data Source = {options.DbService}; User Id = {options.DbUser}; Password = {options.DbPassword}");
            dbConnection.Open();

            var dbCommandFactory = new InputSqlCommandFactory(dbConnection);
            var dbReaderList = dbCommandFactory.CreateDatasetReaders(scriptType, inputSqlText, options.InputSqlArguments);

            var outputEncoder = new UTF8Encoding(false); // 2do! encoding as cmdln argument

            foreach (OracleDataReader dbReader in dbReaderList)
            {
                using (dbReader)
                {
                    while (dbReader.Read())
                    {
                        if (dbReader.FieldCount != 2)
                            throw new InvalidDataException($"Dataset field count is {dbReader.FieldCount}, should be exactly 2");

                        var fieldOneTypeName = dbReader.GetFieldType(0).Name;
                        if (fieldOneTypeName != "string")
                            throw new InvalidDataException($"Field #1 is of type \"{fieldOneTypeName}\", but \"string\" expected");

                        var fieldTwoTypeName = dbReader.GetProviderSpecificFieldType(1).Name;
                        if (fieldTwoTypeName != "OracleClob" && fieldTwoTypeName != "OracleBlob")
                            throw new InvalidDataException($"Field #2 is of type \"{fieldTwoTypeName}\", but LOB expected");

                        var fileName = dbReader.GetString(0);
                        using var outFile = new FileStream(fileName, FileMode.Create, FileAccess.Write);

                        if (fieldTwoTypeName == "OracleClob")
                        {
                            using var lobContents = dbReader.GetOracleClob(1);
                            Console.WriteLine($"Saving {lobContents.Length / 2} characters to {fileName}");
                            using var outFileRecoded = new CryptoStream(outFile, new CharsetEncoderForClob(outputEncoder), CryptoStreamMode.Write, true);
                            lobContents.CorrectlyCopyTo(outFileRecoded);
                        }
                        else if (fieldTwoTypeName == "OracleBlob")
                        {
                            using var lobContents = dbReader.GetOracleBlob(1);
                            Console.WriteLine($"Saving {lobContents.Length} bytes to {fileName}");
                            lobContents.CopyTo(outFile);
                        }
                    }
                }
            }

            return 0;
        }

        internal static StreamReader OpenInputSqlScript(string inputSqlScriptFile)
        {
            return inputSqlScriptFile switch
            {
                "" => new StreamReader(Console.OpenStandardInput()),
                _ => File.OpenText(inputSqlScriptFile)
            };
        }
    }
}

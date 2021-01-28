﻿namespace OraLobUnload
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using CommandLine;
    using Oracle.ManagedDataAccess.Client;
    using OraLobUnload.StreamColumnProcessors;

    internal static class Program
    {
        #pragma warning disable SA1500 // Braces for multi-line statements should not share line
        internal static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<CommandLineOptions>(args)
                .MapResult<CommandLineOptions, int>(
                    options => MainWithOptions(options),
                    _ => {
                        Console.WriteLine("Something bad happened on the command line"); // 2do! be more specific!
                        return 255;
                    });
        }
        #pragma warning restore SA1500 // Braces for multi-line statements should not share line

        internal static int MainWithOptions(CommandLineOptions options)
        {
            ValidateCommandLineArguments(options);
            Console.WriteLine($"note: Using {options.OutputEncoding.HeaderName} for encoding of output CLOBs");

            using StreamReader inputSqlScriptReader = OpenInputSqlScript(options.InputSqlScriptFile);

            using var dbConnection = OracleConnectionFactory(options.DbService, options.DbUser, options.DbPassword);
            dbConnection.Open();

            var dbCommandFactory = new InputSqlCommandFactory(dbConnection);
            IEnumerable<OracleCommand> dbCommandList = dbCommandFactory.CreateDbCommands(options.GetUltimateScriptType(), inputSqlScriptReader);

            foreach (OracleCommand dbCommand in dbCommandList)
            {
                using (dbCommand)
                {
                    using OracleDataReader dbReader = dbCommand.ExecuteReader(System.Data.CommandBehavior.Default);

                    int leastDatasetColumnCountNeeded = Math.Max(options.FileNameColumnIndex, options.LobColumnIndex);
                    if (dbReader.FieldCount < leastDatasetColumnCountNeeded)
                        throw new InvalidDataException($"Dataset field count is {dbReader.FieldCount}, should be at least {leastDatasetColumnCountNeeded}");

                    string fileNameColumnTypeName = dbReader.GetFieldType(options.FileNameColumnIndex - 1).Name;
                    if (fileNameColumnTypeName != "String")
                        throw new InvalidDataException($"Supposed file name column #{options.FileNameColumnIndex} is of type \"{fileNameColumnTypeName}\", but \"string\" expected");

                    SaveDataFromReader(
                        dbReader,
                        options.FileNameColumnIndex - 1,
                        options.LobColumnIndex - 1,
                        StreamColumnProcessorFactory(
                            dbReader.GetProviderSpecificFieldType(options.LobColumnIndex - 1),
                            $"# {options.LobColumnIndex - 1} ({dbReader.GetName(options.LobColumnIndex - 1)})",
                            options.OutputEncoding
                        ),
                        options.OutputFileExtension
                    );
                }
            }

            return 0;
        }

        internal static StreamReader OpenInputSqlScript(string? inputSqlScriptFile)
        {
            return inputSqlScriptFile switch
            {
                "" or null => new StreamReader(Console.OpenStandardInput()),
                _ => File.OpenText(inputSqlScriptFile)
            };
        }

        internal static OracleConnection OracleConnectionFactory(string? dbService, string? dbUser, string? dbPassword)
        {
            if (dbService is null or "")
                throw new ArgumentNullException(nameof(dbService));
            if (dbUser is null or "")
                throw new ArgumentNullException(nameof(dbUser));
            if (dbPassword is null or "")
                throw new ArgumentNullException(nameof(dbPassword));

            return new OracleConnection($"Data Source = {dbService}; User Id = {dbUser}; Password = {dbPassword}");
        }

        internal static void SaveDataFromReader(OracleDataReader dataReader, int fileNameColumnIx, int lobColumnIx, IStreamColumnProcessor processor, string? fileNameExt)
        {
            string cleanedFileNameExt = fileNameExt is not null and not "" ? "." + fileNameExt.Trim('.') : "";
            while (dataReader.Read())
            {
                string fileName = dataReader.GetString(fileNameColumnIx);
                string fileNameWithExt = cleanedFileNameExt != "" && !fileName.EndsWith(cleanedFileNameExt, StringComparison.OrdinalIgnoreCase)
                    ? fileName + cleanedFileNameExt
                    : fileName;
                using Stream outFile = new FileStream(fileNameWithExt, FileMode.Create, FileAccess.Write);

                using Stream lobContents = processor.ReadLob(dataReader, lobColumnIx);
                Console.WriteLine($"Saving a {processor.GetFormattedLobLength(lobContents.Length)} to \"{fileName}\"");
                processor.SaveLobToStream(lobContents, outFile);
            }
        }

        internal static IStreamColumnProcessor StreamColumnProcessorFactory(Type columnType, string columnDescription, Encoding charColumnOutputEncoding)
        {
            return columnType.Name switch
            {
                "OracleClob" => new ClobProcessor(charColumnOutputEncoding),
                "OracleBlob" => new BlobProcessor(),
                "OracleBFile" => new BFileProcessor(),
                _ => throw new InvalidDataException($"Supposed LOB column {columnDescription} is of type \"{columnType.Name}\", but CLOB, BLOB or BFILE expected")
            };
        }

        internal static void ValidateCommandLineArguments(CommandLineOptions options)
        {
            if (options.FileNameColumnIndex is < 1 or > 1000)
                throw new ArgumentOutOfRangeException(nameof(options.FileNameColumnIndex), "Must be between 1 and 1000 (inclusive)");
            if (options.LobColumnIndex is < 1 or > 1000)
                throw new ArgumentOutOfRangeException(nameof(options.LobColumnIndex), "Must be between 1 and 1000 (inclusive)");
            if (options.LobColumnIndex == options.FileNameColumnIndex)
                throw new ArgumentException($"LOB column index {options.LobColumnIndex} cannot be the same as file name column index {options.FileNameColumnIndex}");

            if (options.DbService is null or "" || options.DbUser is null or "" || options.DbPassword is null or "")
                throw new ArgumentNullException("options.Db*", "Empty or incomplete database credentials supplied");
        }
    }
}

﻿namespace OraLobUnload
{
    using System;
    using System.Collections.Generic;
    using Oracle.ManagedDataAccess.Client;

    internal class InputSqlCommandFactory
    {
        private readonly OracleConnection dbConnection;

        internal InputSqlCommandFactory(OracleConnection dbConnection)
        {
            this.dbConnection = dbConnection;
        }

        internal IEnumerable<OracleCommand> CreateDbCommands(InputSqlReturnTypeEnum returnType, string command, IEnumerable<string> inputArguments)
        {
            var result = returnType switch
            {
                InputSqlReturnTypeEnum.Table => CreateCommandTable(command),
                _ => throw new NotImplementedException($"Using input script type \"{returnType}\" not (yet) implemented!")
            };

            return result;
        }

        internal IEnumerable<OracleCommand> CreateCommandTable(string command)
        {
            // 2do! iterate through tables on input
            string cleanedUpTableName = command.Trim().ToUpper();
            Console.WriteLine($"Reading data from table \"{cleanedUpTableName}\"");

            OracleCommand result = new OracleCommand(cleanedUpTableName, this.dbConnection)
            {
                CommandType = System.Data.CommandType.TableDirect,
                FetchSize = 100,
                InitialLOBFetchSize = 262144
            };

            yield return result;
        }
    }
}

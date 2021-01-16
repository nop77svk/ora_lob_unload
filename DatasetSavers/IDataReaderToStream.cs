﻿namespace OraLobUnload.DatasetSavers
{
    using System;
    using System.IO;
    using Oracle.ManagedDataAccess.Client;

    internal interface IDataReaderToStream
    {
        internal Stream ReadLob(OracleDataReader dataReader, int fieldIndex);

        internal long GetTrueLength(long reportedLength);

        internal void SaveLobToStream(Stream inLob, Stream outFile);
    }
}

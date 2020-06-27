using System.Collections.Generic;

namespace ParseProcs
{
	partial class Program
	{
		static void Main (string[] args)
		{
			string ConnectionString = "server=127.0.0.1;port=5432;database=dummy01;uid=postgres;pwd=Yakunichev";

			Dictionary<string, Table> TablesDict = new Dictionary<string, Table> ();
			Dictionary<string, Procedure> ProceduresDict = new Dictionary<string, Procedure> ();

			ReadDatabase (ConnectionString, TablesDict, ProceduresDict);
		}
	}
}

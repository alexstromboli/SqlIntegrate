using System;
using System.Diagnostics;

using Npgsql;

using SqlIntegrate;

namespace TryWrapper
{
	// What a tracker is for: a timestamp on the way in, another on the way out, and the
	// difference logged. Everything it needs per call lives on Call, so one instance
	// serves the whole DbProc and nothing is kept between calls.
	public class SampleTracker : IDbProcTracker<SampleTracker.Call>
	{
		public class Call : IDisposable
		{
			public string Schema;
			public string Procedure;
			public long EnteredAt;
			public int Rows;

			public void Dispose ()
			{
			}
		}

		public Call OnEnter (string Schema, string Procedure)
		{
			return new Call
			{
				Schema = Schema,
				Procedure = Procedure,
				EnteredAt = Stopwatch.GetTimestamp ()
			};
		}

		public void OnBeforeExecute (Call Tracker, NpgsqlCommand Command)
		{
		}

		public void OnAfterExecute (Call Tracker, NpgsqlCommand Command, int RowsAffected)
		{
		}

		public void OnBeforeFetchCursor (Call Tracker, string CursorName, NpgsqlCommand Command)
		{
		}

		public void OnAfterFetchCursor (Call Tracker, string CursorName, NpgsqlCommand Command, int RowCount)
		{
			Tracker.Rows += RowCount;
		}

		public void OnExit (Call Tracker)
		{
			double ms = (Stopwatch.GetTimestamp () - Tracker.EnteredAt) * 1000.0 / Stopwatch.Frequency;
			Console.WriteLine ($"tracked: {Tracker.Schema}.{Tracker.Procedure} took {ms:F1} ms, {Tracker.Rows} row(s)");
		}
	}
}

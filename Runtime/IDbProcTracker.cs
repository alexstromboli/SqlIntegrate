using System;

using Npgsql;

namespace SqlIntegrate
{
	/*
	 * Observes the calls a generated wrapper makes, without taking part in them.
	 *
	 * One instance serves a whole DbProc. T is the per-call state the tracker hands
	 * itself: made on entry, passed to every other method, disposed on exit. Schema
	 * and Procedure are the native names, the ones that go into the command text, so
	 * what a tracker reports lines up with what the database logged.
	 *
	 * These run in line with the procedure, on the calling thread. An implementation
	 * must not throw and must not alter the call.
	 */
	public interface IDbProcTracker<T> where T : IDisposable
	{
		T OnEnter (string Schema, string Procedure);
		void OnBeforeExecute (T Tracker, NpgsqlCommand Command);
		void OnAfterExecute (T Tracker, NpgsqlCommand Command, int RowsAffected);
		void OnBeforeFetchCursor (T Tracker, string CursorName, NpgsqlCommand Command);
		void OnAfterFetchCursor (T Tracker, string CursorName, NpgsqlCommand Command, int RowCount);
		void OnExit (T Tracker);
	}
}

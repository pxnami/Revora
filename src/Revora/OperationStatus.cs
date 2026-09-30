using System;

namespace Revora
{
    internal enum OperationKind { Idle, EnteringRecovery, ExitingRecovery, PreparingFirmware, InstallingFirmware, ConfiguringTools }
    internal sealed class OperationStatus
    {
        public OperationKind Kind { get; private set; }
        public bool Running { get; private set; }
        public string Title { get; private set; }
        public string Stage { get; set; }
        public string Error { get; private set; }
        public string Explanation { get; private set; }
        public int? Progress { get; set; }
        public DateTime Started { get; private set; }
        public void Begin(OperationKind kind, string title) { Kind = kind; Title = title; Stage = title; Running = true; Error = ""; Explanation = null; Progress = null; Started = DateTime.Now; }
        public void Finish() { Running = false; }
        public void Fail(string error, string explanation = null) { Error = error; Explanation = explanation; Running = false; }
    }
    internal sealed class ActivityEntry
    {
        public DateTime Time { get; private set; }
        public string Title { get; private set; }
        public string Detail { get; private set; }
        public ActivityEntry(DateTime time, string title, string detail) { Time = time; Title = title; Detail = detail; }
    }
}

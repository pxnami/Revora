using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Revora
{
    public sealed partial class MainForm
    {
        private Label activityOperation;
        private TableLayoutPanel activityHistory;
        private Panel activityDetails;
        private TableLayoutPanel activityLayout;
        private RevoraButton activityToggle;

        private Control CreateActivity()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0) };
            activityLayout = root;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
            activityOperation = Body("No operation in progress.", 20);
            root.Controls.Add(activityOperation, 0, 0);
            activityHistory = Stack();
            root.Controls.Add(ScrollPage(activityHistory), 0, 1);
            activityToggle = new RevoraButton("Show technical details", ButtonKind.Quiet) { Width = 200 };
            activityToggle.Click += (s, e) => SetActivityDetails(!activityDetails.Visible);
            root.Controls.Add(Actions(activityToggle), 0, 2);
            activityDetails = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Theme.Surface, Visible = false };
            activityDetails.Controls.Add(technicalLog);
            root.Controls.Add(activityDetails, 0, 3);
            root.RowStyles[3].Height = 0;
            return root;
        }

        private void SetActivityDetails(bool visible)
        {
            activityDetails.Visible = visible;
            activityLayout.RowStyles[3].Height = visible ? Theme.Px(activityDetails, 210) : 0;
            activityToggle.Text = visible ? "Hide technical details" : "Show technical details";
            activityToggle.AccessibleName = activityToggle.Text;
        }

        private void ShowActivityDetails() { ShowPage("Activity"); SetActivityDetails(true); }

        private void RenderActivityOperation()
        {
            if (activityOperation == null) return;
            activityOperation.Text = operation.Running ? operation.Title + " · started " + operation.Started.ToString("HH:mm:ss") + "\n" + operation.Stage + (operation.Progress.HasValue ? " · " + operation.Progress + "% of this stage" : "") : "Session activity · " + activity.Count + " events";
        }

        private void RenderActivity()
        {
            if (activityHistory == null) return;
            RenderActivityOperation();
            activityHistory.SuspendLayout();
            foreach (var control in activityHistory.Controls.Cast<Control>().ToArray()) { activityHistory.Controls.Remove(control); control.Dispose(); }
            activityHistory.RowStyles.Clear();
            activityHistory.RowCount = 0;
            if (activity.Count == 0) activityHistory.Controls.Add(Body("Device connections and operations appear here as they happen."));
            foreach (var entry in activity.AsEnumerable().Reverse()) {
                var label = new Label { AutoSize = true, Text = entry.Time.ToString("HH:mm:ss") + "   " + entry.Title + (entry.Detail.Length == 0 ? "" : "\n" + entry.Detail),
                    Font = Theme.Font(10F), ForeColor = Theme.Ink, MaximumSize = new Size(System.Math.Max(200, activityHistory.Width - 20), 0), Margin = new Padding(0, 0, 0, 22) };
                activityHistory.Controls.Add(label);
            }
            activityHistory.ResumeLayout(true);
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Revora
{
    public sealed partial class MainForm
    {
        private Label recoveryTitle, recoveryDescription, recoverySupport;
        private StatusIndicator recoveryStatus;
        private RevoraButton recoveryAction, recoveryFirmware;
        private Label dfuGuide;

        private Control CreateRecovery()
        {
            var body = Stack();
            recoveryTitle = Heading("Recovery Mode");
            recoveryDescription = Body("Connect your device to use recovery tools.");
            recoveryStatus = new StatusIndicator { Width = 300, Height = 32, Margin = new Padding(0, 0, 0, 24) };
            recoveryAction = new RevoraButton("Enter Recovery Mode", ButtonKind.Primary);
            recoveryAction.Click += async (s, e) => await RecoveryAsync();
            recoveryFirmware = new RevoraButton("Go to Firmware");
            recoveryFirmware.Click += (s, e) => ShowPage("Firmware");
            recoverySupport = Body("Recovery Mode restarts your device into Apple’s restore environment. Entering it doesn’t install firmware or erase data.");
            dfuGuide = Body("", 24);
            body.Controls.Add(recoveryTitle);
            body.Controls.Add(recoveryDescription);
            body.Controls.Add(recoveryStatus);
            body.Controls.Add(Actions(recoveryAction, recoveryFirmware));
            body.Controls.Add(recoverySupport);
            body.Controls.Add(dfuGuide);
            var instructions = new LinkLabel { Text = "Apple’s official force-restart instructions", AutoSize = true, LinkColor = Theme.Accent, ActiveLinkColor = Theme.Accent, Margin = new Padding(0, 8, 0, 0) };
            instructions.LinkClicked += (s, e) => OpenLink(SelectedDevice != null && SelectedDevice.IsIpad ? "https://support.apple.com/en-us/102642" : "https://support.apple.com/guide/iphone/iph8903c3ee6/ios");
            body.Controls.Add(instructions);
            return ScrollPage(body);
        }

        private void RenderRecovery()
        {
            if (recoveryTitle == null) return;
            var device = SelectedDevice;
            bool dfu = DeviceReady && device.Mode == DeviceMode.Dfu;
            bool recovering = DeviceReady && device.Mode == DeviceMode.Recovery;
            recoveryTitle.Text = dfu ? "DFU Mode" : "Recovery Mode";
            recoveryDescription.Text = !DeviceReady ? "Connect a device to use recovery tools."
                : dfu ? "Your device is in Device Firmware Update mode. Exiting requires a manual force restart."
                : recovering ? "Your device is currently in Recovery Mode. Request a restart into normal operation."
                : "Restart your device into Apple’s recovery environment.";
            recoveryStatus.Set(monitor.Snapshot.Connection, device, operation.Running ? operation.Title : null);
            recoveryAction.Visible = !dfu;
            recoveryAction.Text = recovering ? "Exit Recovery Mode" : "Enter Recovery Mode";
            recoveryAction.Enabled = DeviceReady && !operation.Running && (device.Mode == DeviceMode.Normal || device.Ecid != 0);
            recoveryFirmware.Enabled = DeviceReady && !operation.Running;
            recoverySupport.Visible = !dfu;
            dfuGuide.Visible = dfu;
            dfuGuide.Text = !dfu ? "" : device.IsIpad
                ? "iPad without a Home button\n1. Quickly press and release the volume button closest to the top button.\n2. Quickly press and release the other volume button.\n3. Hold the top button until the Apple logo appears.\n\niPad with a Home button\nHold the Home and top buttons until the Apple logo appears."
                : "iPhone 8 and later\n1. Quickly press and release Volume Up, then Volume Down.\n2. Hold the side button until the Apple logo appears.\n\niPhone 7\nHold Volume Down and the side button until the Apple logo appears.\n\niPhone 6s and earlier\nHold the Home and side/top buttons until the Apple logo appears.";
        }
    }
}

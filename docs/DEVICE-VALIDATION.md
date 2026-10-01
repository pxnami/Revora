# Device validation

Use backed-up devices you own and can erase. Record the Windows version, Apple driver version, device model, iOS version, and bundled native revisions.

1. Start Revora without a device. Confirm a useful disconnected state and no enabled device actions.
2. Connect a locked device. Verify a trust or connection error is visible. Unlock it, accept Trust, and refresh.
3. Confirm the name, product, hardware, ECID, and OS match the device. Unplug it and verify actions disable after a scan.
4. Enter recovery, refresh, then exit recovery and verify the device returns to normal mode. Repeat with an iPad.
5. Connect a DFU device. Confirm its mode is shown and Exit recovery is disabled. Perform a manual force restart.
6. Choose an IPSW for a different model and a matching product with a different hardware identity. Both must be rejected before the restore executable launches.
7. On a test device, choose matching, currently signed firmware with an Update variant. Confirm the data-loss acknowledgement is required. Verify install progress and final device boot. Check whether existing data was preserved.
8. On an erasable device, select erase restore. Verify both acknowledgement and exact `ERASE` text are required. Verify install completion, activation, and erased data.
9. Disconnect or swap the selected device before confirmation. The fresh identity check must abort; another connected device must never be restored.
10. Check unsigned firmware failure and a USB failure during restore. Do not deliberately unplug a valuable device. Confirm logs and errors are useful and closing the app is blocked while a restore runs.
11. Check actual 100%, 125%, 150%, 175%, and 200% Windows display scaling, including movement between monitors. Check keyboard navigation, visible focus, small windows, scrolling, every page/modal, missing native tools, and a tools path containing spaces. Automated scale fixtures do not replace physical monitor-DPI checks.
12. Verify the complete release archive on a clean Windows computer without MSYS2 installed. Native tools must launch using only the bundled DLLs and installed Apple drivers.

Keep the release marked experimental until the relevant device and clean-machine checks pass. Do not claim universal model support or data preservation based on unit tests.

## Device-state regression checks

- Leave a connected device in normal mode for at least three ten-second monitoring cycles. The device screen, enabled actions and connection status must stay stable without “Checking for devices” flicker.
- Unplug and reconnect the same device. The selected context must clear on disconnect and return from reported USB data after reconnecting.
- Enter and exit Recovery Mode. Confirm identity remains associated by ECID, the primary action changes automatically, and retained information is labeled as cached.
- Open and close Device Details repeatedly and switch between every page. Neither action should create another scan loop.
- Press Refresh repeatedly while a scan is pending. Only one scan may run at a time.
- Change or disconnect the device before confirmation. Firmware selection and destructive acknowledgement must reset.
- During an installation failure, remain on the result/operation screen with technical details in Activity. Do not silently return to Home or report success.

The automated checks cover these transitions with synthetic device/tool responses. A read-only scan on the development machine returned no connected devices, so physical recovery and restore were not exercised.

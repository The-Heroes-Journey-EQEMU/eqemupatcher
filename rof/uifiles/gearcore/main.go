package main

import (
	_ "embed"
	"fmt"
	"os"
	"path/filepath"
	"syscall"

	"github.com/xackery/shinspark/config"
	"github.com/xackery/wlk/cpl"
	"github.com/xackery/wlk/walk"
	"github.com/xackery/wlk/win"
)

var (
	cfg                    *config.CritSprinklerConfiguration
	settingsWnd            *walk.MainWindow
	options                map[string]*windowOption
	wndPlayerDefaultOption *walk.RadioButton
	wndPlayerExpOption     *walk.RadioButton
	wndPlayerAAOption      *walk.RadioButton
)

type windowOption struct {
	option *walk.RadioButton
	path   string
}

func main() {
	err := run()
	if err != nil {
		fmt.Printf("Error: %v\n", err)
		os.Exit(1)
	}
}

func run() error {
	var err error
	exePath := os.Args[0]

	wd := filepath.Dir(exePath)

	cfg, err = config.LoadShinSparkConfig(wd + "/settings.ini")
	if err != nil {
		fmt.Printf("load settings: %v\n", err)
	}

	options = map[string]*windowOption{
		"player_default": {path: "player/EQUI_PlayerWindow.xml"},
		"player_exp":     {path: "player/exp/EQUI_PlayerWindow.xml"},
		"player_aa_exp":  {path: "player/aa/EQUI_PlayerWindow.xml"},
	}

	cmw := cpl.MainWindow{
		Title:    "Shin Spark Config",
		Name:     "settings",
		AssignTo: &settingsWnd,
		Size:     cpl.Size{Width: cfg.SettingsW, Height: cfg.SettingsH},
		Layout:   cpl.VBox{},

		Children: []cpl.Widget{
			cpl.LinkLabel{
				OnLinkActivated: func(link *walk.LinkLabelLink) {
					urlPtr := syscall.StringToUTF16Ptr(link.URL())
					verbPtr := syscall.StringToUTF16Ptr("open")
					win.ShellExecute(0, verbPtr, urlPtr, nil, nil, win.SW_SHOWNORMAL)
				},
				Text: `<a id="youtube" href="https://youtu.be/Ig_GnNWxPdU">Watch Walkthrough</a>`,
			},
			cpl.GroupBox{
				Title:     "Player Window",
				Layout:    cpl.VBox{},
				Alignment: cpl.AlignHVDefault,
				Children: []cpl.Widget{
					cpl.RadioButton{Text: "Default", AssignTo: &options["player_default"].option, Enabled: true},
					cpl.RadioButton{Text: "Show Exp", AssignTo: &options["player_exp"].option},
					cpl.RadioButton{Text: "Show AA Exp", AssignTo: &options["player_aa_exp"].option},
				},
			},
			cpl.PushButton{
				Text:    "Save",
				MaxSize: cpl.Size{Width: 45},
				OnClicked: func() {
					err := updateSave()
					if err != nil {
						fmt.Printf("Error: %v\n", err)
					}
					os.Exit(0)

				},
			},
		},
		OnSizeChanged: func() {

		},
		OnMouseMove: func(x, y int, button walk.MouseButton) {
		},

		OnBoundsChanged: func() {
		},

		Visible: cfg.LogPath == "",
	}

	err = cmw.Create()
	if err != nil {
		return fmt.Errorf("create main window: %w", err)
	}

	if !cfg.IsNew {
		settingsWnd.SetX(cfg.SettingsX)
		settingsWnd.SetY(cfg.SettingsY)
	}
	settingsWnd.SetWidth(cfg.SettingsW)
	settingsWnd.SetHeight(cfg.SettingsH)

	settingsWnd.Closing().Attach(func(isCancel *bool, reason byte) {
		err := updateSave()
		if err != nil {
			walk.MsgBox(settingsWnd, "Error", err.Error(), walk.MsgBoxIconError)
		}
	})

	code := settingsWnd.Run()
	if code != 0 {
		fmt.Printf("mainWalk Error: %v\n", code)
	}

	return nil
}

func updateSave() error {
	if cfg == nil {
		return fmt.Errorf("config not loaded")
	}

	cfg.SettingsH = settingsWnd.Height()
	cfg.SettingsW = settingsWnd.Width()
	cfg.SettingsX = settingsWnd.X()
	cfg.SettingsY = settingsWnd.Y()

	err := cfg.Save()
	if err != nil {
		return fmt.Errorf("save config: %w", err)
	}

	return nil
}
func lastErrorMessage() string {
	return errorMessage(win.GetLastError())
}

func errorMessage(errCode uint32) string {
	var buf [512]uint16
	n, err := syscall.FormatMessage(
		syscall.FORMAT_MESSAGE_FROM_SYSTEM|syscall.FORMAT_MESSAGE_IGNORE_INSERTS,
		0,
		errCode,
		0, // Default language
		buf[:],
		nil,
	)
	if err != nil {
		return fmt.Sprintf("Unknown error code %d", errCode)
	}
	return syscall.UTF16ToString(buf[:n])
}

﻿using Lively.Common.Helpers.Pinvoke;
using Lively.Grpc.Client;
using Lively.Models;
using Lively.UI.WinUI.ViewModels;
using Lively.UI.WinUI.Views;
using Lively.UI.WinUI.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using SettingsUI.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Lively.UI.WinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly List<(Type Page, NavPages NavPage)> _pages = new List<(Type Page, NavPages NavPage)>
        {
            (typeof(LibraryView), NavPages.library),
            (typeof(GalleryView), NavPages.gallery),
            (typeof(SettingsGeneralView), NavPages.settingsGeneral),
            (typeof(SettingsPerformanceView), NavPages.settingsPerformance),
            (typeof(SettingsWallpaperView), NavPages.settingsWallpaper),
            (typeof(SettingsSystemView), NavPages.settingsSystem),
        };

        private readonly SettingsViewModel settingsVm;
        private readonly IDesktopCoreClient desktopCore;
        private readonly IUserSettingsClient userSettings;
        private readonly LibraryViewModel libraryVm;
        private readonly ResourceLoader i18n;

        public MainWindow(IDesktopCoreClient desktopCore,
            IUserSettingsClient userSettings,
            SettingsViewModel settingsVm,
            LibraryViewModel libraryVm,
            IAppUpdaterClient appUpdater)
        {
            this.settingsVm = settingsVm;
            this.desktopCore = desktopCore;
            this.libraryVm = libraryVm;
            this.userSettings = userSettings;

            this.InitializeComponent();
            i18n = ResourceLoader.GetForViewIndependentUse();
            this.audioSlider.Value = settingsVm.GlobalWallpaperVolume;
            UpdateAudioSliderIcon(settingsVm.GlobalWallpaperVolume);
            this.controlPanelLabel.Label = $"{desktopCore.Wallpapers.Count} {i18n.GetString("ActiveWallpapers/Label")}";
            desktopCore.WallpaperChanged += DesktopCore_WallpaperChanged;
            desktopCore.WallpaperError += DesktopCore_WallpaperError;
            appUpdater.UpdateChecked += AppUpdater_UpdateChecked;
            //App startup is slower if done in NavView_Loaded..
            CreateMainMenu();
            NavViewNavigate(NavPages.library);

            //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/6070
            //ExtendsContentIntoTitleBar = true;
            //SetTitleBar(TitleBar);

            //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/4056
            this.Title = "Lively Wallpaper";
            this.SetIconEx("appicon.ico");

            _ = StdInListener();
        }

        private void DesktopCore_WallpaperError(object sender, Exception e)
        {
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                infoBar.IsOpen = true;
                infoBar.ActionButton = new HyperlinkButton
                {
                    Content = i18n.GetString("Help.Label"),
                    NavigateUri = new Uri("https://github.com/rocksdanister/lively/wiki/Common-Problems"),
                };
                infoBar.Title = i18n.GetString("TextError");
                infoBar.Message = e.Message;
                infoBar.Severity = InfoBarSeverity.Error;
            });
        }

        private void AppUpdater_UpdateChecked(object sender, Common.Services.AppUpdaterEventArgs e)
        {
            if (e.UpdateStatus == Common.Services.AppUpdateStatus.available)
            {
                _ = this.DispatcherQueue.TryEnqueue(() =>
                {
                    infoBar.IsOpen = true;
                    var btn = new Button()
                    {
                        Content = i18n.GetString("TextLearnMore"),
                    };
                    btn.Click += (_, _) =>
                    {
                        infoBar.IsOpen = false;
                        ShowAboutDialog();
                    };
                    infoBar.ActionButton = btn;
                    infoBar.Title = i18n.GetString("TextUpdateAvailable");
                    infoBar.Message = $"{i18n.GetString("DescriptionUpdateAvailable")} (v{e.UpdateVersion})";
                    infoBar.Severity = InfoBarSeverity.Success;
                });
            }
        }

        private void DesktopCore_WallpaperChanged(object sender, EventArgs e)
        {
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                //wallpaper focus steal fix.
                if (this.Visible)
                {
                    if (!userSettings.Settings.ControlPanelOpened)
                    {
                        toggleTeachingTipControlPanel.IsOpen = true;
                        userSettings.Settings.ControlPanelOpened = true;
                        userSettings.Save<ISettingsModel>();
                    }
                    NativeMethods.SetForegroundWindow(this.GetWindowHandleEx());
                    //this.Activate();
                }
                controlPanelLabel.Label = $"{desktopCore.Wallpapers.Count} {i18n.GetString("ActiveWallpapers/Label")}";
            });
        }

        public void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                CreateSettingsMenu();
                NavViewNavigate(NavPages.settingsGeneral);
            }
            else if (args.InvokedItemContainer != null)
            {
                var navItemTag = args.InvokedItemContainer.Tag.ToString();
                NavigatePage(navItemTag);
            }
        }

        public void NavViewNavigate(NavPages item)
        {
            var tag = GetEnumMemberAttrValue(item);
            navView.SelectedItem = navView.MenuItems.First(x => ((NavigationViewItem)x).Tag.ToString() == tag);
            NavigatePage(tag);
        }

        private void NavigatePage(string navItemTag)
        {
            var item = _pages.FirstOrDefault(p => GetEnumMemberAttrValue(p.NavPage).Equals(navItemTag));
            Type _page = item.Page;
            // Get the page type before navigation so you can prevent duplicate entries in the backstack.
            var preNavPageType = contentFrame.CurrentSourcePageType;
            // Only navigate if the selected page isn't currently loaded.
            if (!(_page is null) && !Type.Equals(preNavPageType, _page))
            {
                contentFrame.Navigate(_page, null, new DrillInNavigationTransitionInfo());
            }
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            CreateMainMenu();
            NavViewNavigate(NavPages.library);

            //If audio changed in settings page..
            this.audioSlider.Value = settingsVm.GlobalWallpaperVolume;
        }

        private async void AddWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            var addVm = App.Services.GetRequiredService<AddWallpaperViewModel>();
            var addDialog = new ContentDialog()
            {
                Title = i18n.GetString("AddWallpaper/Label"),
                Content = new AddWallpaperView(addVm),
                PrimaryButtonText = i18n.GetString("TextClose"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
            };
            addVm.OnRequestClose += (_, _) => addDialog.Hide();
            await addDialog.ShowAsyncQueue();

            if (addVm.NewWallpaper != null)
            {
                await desktopCore.SetWallpaper(addVm.NewWallpaper, userSettings.Settings.SelectedDisplay);
                /*
                var inputVm = new AddWallpaperDataViewModel(addVm.NewWallpaper);
                var inputDialog = new ContentDialog()
                {
                    Title = i18n.GetString("AddWallpaper/Label"),
                    Content = new AddWallpaperDataView(inputVm),
                    PrimaryButtonText = i18n.GetString("TextOk"),
                    SecondaryButtonText = i18n.GetString("Cancel/Content"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot,
                    SecondaryButtonCommand = inputVm.CancelCommand,
                    PrimaryButtonCommand = inputVm.ProceedCommand,
                };          
                await inputDialog.ShowAsyncQueue();
                */
            }
        }

        private void ControlPanelButton_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog()
            {
                Title = i18n.GetString("DescriptionScreenLayout"),
                Content = new ScreenLayoutView(),
                PrimaryButtonText = i18n.GetString("TextOK"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
            }.ShowAsyncQueue();
        }

        private void AppBarHelpButton_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog()
            {
                Title = i18n.GetString("Help/Label"),
                Content = new HelpView(),
                PrimaryButtonText = i18n.GetString("TextOK"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
            }.ShowAsyncQueue();
        }

        private void AppBarAboutButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAboutDialog();
        }

        private void ShowAboutDialog()
        {
            _ = new ContentDialog()
            {
                Title = i18n.GetString("About/Label"),
                Content = new AboutView(),
                PrimaryButtonText = i18n.GetString("TextOK"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
            }.ShowAsyncQueue();
        }

        private void SliderAudio_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            settingsVm.GlobalWallpaperVolume = (int)e.NewValue;
            UpdateAudioSliderIcon(settingsVm.GlobalWallpaperVolume);
        }

        private void CreateMainMenu()
        {
            navView.MenuItems.Clear();
            navView.FooterMenuItems.Clear();
            navView.IsSettingsVisible = true;
            navCommandBar.Visibility = Visibility.Visible;
            navView.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
            navView.MenuItems.Add(CreateMenu(i18n.GetString("TitleLibrary"), "library", "\uE8A9"));
            navView.MenuItems.Add(CreateMenu(i18n.GetString("TitleGallery"), "gallery", "\uE719"));
        }

        private void CreateSettingsMenu()
        {
            navView.MenuItems.Clear();
            navView.FooterMenuItems.Clear();
            navView.IsSettingsVisible = false;
            navCommandBar.Visibility = Visibility.Collapsed;
            navView.IsBackButtonVisible = NavigationViewBackButtonVisible.Visible;
            navView.MenuItems.Add(CreateMenu(i18n.GetString("TitleGeneral"), "general"));
            navView.MenuItems.Add(CreateMenu(i18n.GetString("TitlePerformance"), "performance"));
            navView.MenuItems.Add(CreateMenu(i18n.GetString("TitleWallpaper"), "wallpaper"));
            navView.MenuItems.Add(CreateMenu(i18n.GetString("System/Header"), "system"));
        }

        private void UpdateAudioSliderIcon(double volume) => 
            audioBtn.Icon = audioIcons[(int)Math.Ceiling((audioIcons.Length - 1) * volume / 100)];

        //Actually called before window closed!
        //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/5454
        private async void Window_Closed(object sender, WindowEventArgs args)
        {
            if (userSettings.Settings.IsFirstRun)
            {
                args.Handled = true;
                await new ContentDialog()
                {
                    Title = i18n.GetString("PleaseWait/Text"),
                    Content = new TrayMenuHelp(),
                    PrimaryButtonText = i18n.GetString("TextOK"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot,
                }.ShowAsyncQueue();
                userSettings.Settings.IsFirstRun = false;
                userSettings.Save<ISettingsModel>();
                this.Close();
            }

            if (userSettings.Settings.KeepAwakeUI)
            {
                args.Handled = true;
                NativeMethods.ShowWindow(this.GetWindowHandleEx(), (uint)NativeMethods.SHOWWINDOW.SW_HIDE);
            }
            else
            {
                App.ShutDown();
            }
        }

        /// <summary>
        /// std I/O redirect.
        /// </summary>
        private async Task StdInListener()
        {
            try
            {
                await Task.Run(async () =>
                {
                    while (true)
                    {
                        var msg = await Console.In.ReadLineAsync();
                        if (string.IsNullOrEmpty(msg))
                        {
                            //When the redirected stream is closed, a null line is sent to the event handler. 
                            break;
                        }
                        var args = msg.Split(' ');
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (args[0].Equals("WM", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args[1].Equals("SHOW", StringComparison.OrdinalIgnoreCase))
                                {
                                    //this.Show();
                                    NativeMethods.ShowWindow(this.GetWindowHandleEx(), (uint)NativeMethods.SHOWWINDOW.SW_SHOW);
                                }
                            }
                            else if (args[0].Equals("LM", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args[1].Equals("SHOWBUSY", StringComparison.OrdinalIgnoreCase))
                                {
                                    libraryVm.IsBusy = true;
                                }
                                else if (args[1].Equals("HIDEBUSY", StringComparison.OrdinalIgnoreCase))
                                {
                                    libraryVm.IsBusy = false;
                                }
                                /*
                                if (args[1].Equals("SHOWCONTROLPANEL", StringComparison.OrdinalIgnoreCase))
                                {
                                    //this.ShowControlPanelDialog();
                                }
                                else if (args[1].Equals("SHOWCUSTOMISEPANEL", StringComparison.OrdinalIgnoreCase))
                                {
                                    var items = desktopCore.Wallpapers.Where(x => x.LivelyPropertyCopyPath != null);
                                    if (items.Count() != 0)
                                    {
                                        //Usually this msg is sent when span/duplicate layout mode.
                                        var model = libraryVm.LibraryItems.FirstOrDefault(x => items.First().LivelyInfoFolderPath == x.LivelyInfoFolderPath);
                                        if (model != null)
                                        {
                                            var settingsWidget = new LivelyPropertiesTrayWidget(model);
                                            settingsWidget.Show();
                                        }
                                    }
                                }
                                */
                            }
                        });
                    }
                });
            }
            catch { }
        }

        #region helpers

        public enum NavPages
        {
            [EnumMember(Value = "library")]
            library,
            [EnumMember(Value = "gallery")]
            gallery,
            [EnumMember(Value = "general")]
            settingsGeneral,
            [EnumMember(Value = "performance")]
            settingsPerformance,
            [EnumMember(Value = "wallpaper")]
            settingsWallpaper,
            [EnumMember(Value = "system")]
            settingsSystem,
        }

        public static string GetEnumMemberAttrValue<T>(T enumVal) where T : Enum
        {
            var enumType = typeof(T);
            var memInfo = enumType.GetMember(enumVal.ToString());
            var attr = memInfo.FirstOrDefault()?.GetCustomAttributes(false).OfType<EnumMemberAttribute>().FirstOrDefault();
            return attr?.Value;
        }

        private readonly FontIcon[] audioIcons =
        {
            new FontIcon(){ Glyph = "\uE74F" },
            new FontIcon(){ Glyph = "\uE992" },
            new FontIcon(){ Glyph = "\uE993" },
            new FontIcon(){ Glyph = "\uE994" },
            new FontIcon(){ Glyph = "\uE995" },
        };

        private static NavigationViewItem CreateMenu(string menuName, string tag, string glyph = "")
        {
            var item = new NavigationViewItem
            {
                Name = menuName,
                Content = menuName,
                Tag = tag,
            };
            if (!string.IsNullOrEmpty(glyph))
            {
                item.Icon = new FontIcon()
                {
                    Glyph = glyph
                };
            }
            return item;
        }

        #endregion //helpers
    }
}
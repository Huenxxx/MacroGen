using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacroCreatorApp
{
    public static class GitHubUpdater
    {
        private const string RepoOwner = "Huenxxx";
        private const string RepoName = "MacroCreator";

        public static async Task CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "MacroGen-Updater");
                
                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("tag_name", out var tagElement)) return;
                string tagName = tagElement.GetString() ?? "";
                
                // Limpiar el tag (ej. "v1.2.0" -> "1.2.0")
                string cleanTag = tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tagName.Substring(1) : tagName;
                
                // Intentar parsear las versiones
                if (!Version.TryParse(cleanTag, out Version? latestVersion)) return;
                
                // Obtener versión actual
                Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

                if (latestVersion > currentVersion)
                {
                    if (MessageBox.Show(
                        $"Hay una nueva versión disponible ({tagName}).\n\n¿Quieres descargarla e instalarla ahora?", 
                        "Actualización Disponible - MacroGen", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        var assets = root.GetProperty("assets");
                        string? downloadUrl = null;
                        foreach (var asset in assets.EnumerateArray())
                        {
                            string assetName = asset.GetProperty("name").GetString() ?? "";
                            if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && 
                                assetName.Contains("setup", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                                break;
                            }
                        }

                        if (downloadUrl != null)
                        {
                            await DownloadAndInstallAsync(downloadUrl);
                        }
                        else
                        {
                            // Si no hay .exe, abrir la web
                            string htmlUrl = root.GetProperty("html_url").GetString() ?? "";
                            Process.Start(new ProcessStartInfo(htmlUrl) { UseShellExecute = true });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error comprobando actualizaciones: " + ex.Message);
            }
        }

        private static async Task DownloadAndInstallAsync(string downloadUrl)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), "MacroGen_Update.exe");
                
                // Ventana de progreso visual
                var progressWindow = new Window
                {
                    Title = "Descargando Actualización...",
                    Width = 400,
                    Height = 140,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.ToolWindow,
                    Topmost = true,
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White
                };

                var panel = new StackPanel { Margin = new Thickness(20) };
                var label = new TextBlock 
                { 
                    Text = "Descargando la nueva versión, por favor espera...", 
                    Margin = new Thickness(0,0,0,15),
                    Foreground = Brushes.White,
                    FontSize = 14
                };
                
                var progressBar = new ProgressBar 
                { 
                    Height = 20, 
                    Minimum = 0, 
                    Maximum = 100,
                    Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                    Foreground = new SolidColorBrush(Color.FromRgb(14, 99, 156))
                };
                
                var progressText = new TextBlock 
                { 
                    Text = "0%", 
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    Margin = new Thickness(0,5,0,0),
                    Foreground = Brushes.LightGray
                };
                
                panel.Children.Add(label);
                panel.Children.Add(progressBar);
                panel.Children.Add(progressText);
                progressWindow.Content = panel;
                progressWindow.Show();

                // Descargar con progreso
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "MacroGen-Updater");
                
                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                long? totalBytes = response.Content.Headers.ContentLength;
                
                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                
                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;
                
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    
                    if (totalBytes.HasValue)
                    {
                        double progress = (double)totalRead / totalBytes.Value * 100;
                        progressBar.Value = progress;
                        progressText.Text = $"{progress:F1}% ({(totalRead / 1024.0 / 1024.0):F2} MB / {(totalBytes.Value / 1024.0 / 1024.0):F2} MB)";
                        
                        // Permitir que la UI se actualice
                        await Task.Delay(1); 
                    }
                }

                // Cerrar explícitamente el stream para liberar el archivo antes de ejecutarlo
                await fileStream.DisposeAsync();

                progressWindow.Close();

                // Ejecutar el instalador de forma silenciosa y salir
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempFile,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                    UseShellExecute = true
                });

                // Forzar el cierre inmediato para asegurar que el instalador pueda sobreescribir el .exe
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al descargar la actualización: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

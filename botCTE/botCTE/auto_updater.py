"""
Auto-Updater Module
Handles downloading and installing application updates
"""

import os
import sys
import subprocess
import tempfile
import requests
from pathlib import Path
import shutil
import threading


class AutoUpdater:
    """Handles automatic application updates."""
    
    def __init__(self, download_url=None, app_name="CTe LogLife"):
        """
        Initialize auto-updater.
        
        Args:
            download_url: URL to download the installer
            app_name: Name of the application
        """
        self.download_url = download_url
        self.app_name = app_name
        self.temp_dir = tempfile.gettempdir()
        self.installer_path = None
        self.download_progress = 0
        self.download_callback = None
        
    def set_download_callback(self, callback):
        """
        Set callback function for download progress.
        
        Args:
            callback: Function to call with progress (0-100)
        """
        self.download_callback = callback
    
    def download_installer(self):
        """
        Download the installer from the provided URL.
        
        Returns:
            str: Path to downloaded installer or None if failed
        """
        if not self.download_url:
            print("No download URL provided")
            return None
        
        try:
            # Generate installer filename
            installer_filename = f"{self.app_name.replace(' ', '_')}_Setup.exe"
            installer_path = os.path.join(self.temp_dir, installer_filename)
            
            print(f"Downloading installer from: {self.download_url}")
            print(f"Saving to: {installer_path}")
            
            # Download with progress tracking
            response = requests.get(self.download_url, stream=True, timeout=30)
            response.raise_for_status()
            
            total_size = int(response.headers.get('content-length', 0))
            block_size = 8192
            downloaded_size = 0
            
            with open(installer_path, 'wb') as f:
                for chunk in response.iter_content(chunk_size=block_size):
                    if chunk:
                        f.write(chunk)
                        downloaded_size += len(chunk)
                        
                        # Calculate and report progress
                        if total_size > 0:
                            progress = int((downloaded_size / total_size) * 100)
                            self.download_progress = progress
                            
                            if self.download_callback:
                                self.download_callback(progress)
            
            print(f"Download complete: {installer_path}")
            self.installer_path = installer_path
            return installer_path
            
        except requests.exceptions.RequestException as e:
            print(f"Error downloading installer: {e}")
            return None
        except Exception as e:
            print(f"Unexpected error during download: {e}")
            return None
    
    def verify_installer(self, installer_path):
        """
        Verify that the downloaded installer exists and is valid.
        
        Args:
            installer_path: Path to the installer file
            
        Returns:
            bool: True if valid, False otherwise
        """
        if not installer_path or not os.path.exists(installer_path):
            return False
        
        # Check file size (installer should be at least 1MB)
        file_size = os.path.getsize(installer_path)
        if file_size < 1024 * 1024:  # 1MB minimum
            print(f"Installer file too small: {file_size} bytes")
            return False
        
        return True
    
    def install_update(self, silent=False):
        """
        Install the downloaded update.
        
        Args:
            silent: Whether to run installer in silent mode
            
        Returns:
            bool: True if installation started successfully
        """
        if not self.installer_path:
            print("No installer downloaded")
            return False
        
        if not self.verify_installer(self.installer_path):
            print("Installer verification failed")
            return False
        
        try:
            # Prepare installer arguments
            if silent:
                # Inno Setup silent install flags
                args = [self.installer_path, '/VERYSILENT', '/NORESTART']
            else:
                args = [self.installer_path]
            
            print(f"Starting installer: {' '.join(args)}")
            
            # Start the installer
            subprocess.Popen(args, shell=True)
            
            # Exit current application to allow update
            print("Exiting application for update...")
            return True
            
        except Exception as e:
            print(f"Error starting installer: {e}")
            return False
    
    def download_and_install(self, silent=False, exit_after=True):
        """
        Download and install update in one operation.
        
        Args:
            silent: Whether to run installer in silent mode
            exit_after: Whether to exit application after starting installer
            
        Returns:
            bool: True if successful, False otherwise
        """
        # Download the installer
        installer_path = self.download_installer()
        
        if not installer_path:
            return False
        
        # Install the update
        success = self.install_update(silent=silent)
        
        if success and exit_after:
            # Give installer time to start
            import time
            time.sleep(2)
            sys.exit(0)
        
        return success
    
    def cleanup(self):
        """Clean up downloaded installer files."""
        if self.installer_path and os.path.exists(self.installer_path):
            try:
                os.remove(self.installer_path)
                print(f"Cleaned up installer: {self.installer_path}")
            except Exception as e:
                print(f"Error cleaning up installer: {e}")
    
    def download_in_background(self, completion_callback=None):
        """
        Download installer in background thread.
        
        Args:
            completion_callback: Function to call when download completes (receives installer_path)
        """
        def download_task():
            installer_path = self.download_installer()
            if completion_callback:
                completion_callback(installer_path)
        
        thread = threading.Thread(target=download_task, daemon=True)
        thread.start()
        return thread


class UpdateManager:
    """High-level update manager combining version checking and updating."""
    
    def __init__(self, version_checker, auto_updater=None):
        """
        Initialize update manager.
        
        Args:
            version_checker: VersionChecker instance
            auto_updater: AutoUpdater instance (optional)
        """
        self.version_checker = version_checker
        self.auto_updater = auto_updater
    
    def check_and_prompt_update(self):
        """
        Check for updates and return update information.
        
        Returns:
            dict: Update information from version checker
        """
        return self.version_checker.check_for_updates()
    
    def perform_update(self, update_info, silent=False, progress_callback=None):
        """
        Perform the update process.
        
        Args:
            update_info: Update information from check_and_prompt_update
            silent: Whether to run installer silently
            progress_callback: Function to call with download progress
            
        Returns:
            bool: True if update initiated successfully
        """
        if not update_info.get('update_available'):
            print("No update available")
            return False
        
        download_url = update_info.get('download_url')
        if not download_url:
            print("No download URL available")
            return False
        
        # Create or update auto-updater with download URL
        if not self.auto_updater:
            from version_checker import VersionChecker
            self.auto_updater = AutoUpdater(download_url=download_url)
        else:
            self.auto_updater.download_url = download_url
        
        if progress_callback:
            self.auto_updater.set_download_callback(progress_callback)
        
        # Download and install
        return self.auto_updater.download_and_install(silent=silent)


if __name__ == "__main__":
    # Test the updater
    print("=== Auto-Updater Test ===")
    
    # Example usage
    test_url = "https://example.com/installer.exe"
    updater = AutoUpdater(download_url=test_url)
    
    print(f"Updater initialized with URL: {test_url}")
    print(f"Temp directory: {updater.temp_dir}")

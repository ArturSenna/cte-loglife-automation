"""
Version Checker Module
Checks for application updates from GitHub releases
"""

import requests
import json
from packaging import version
import os


class VersionChecker:
    """Handles version checking against GitHub releases."""
    
    def __init__(self, repo_owner="ArturSenna", repo_name="cte-loglife-automation", current_version="1.0.0"):
        """
        Initialize version checker.
        
        Args:
            repo_owner: GitHub repository owner
            repo_name: GitHub repository name
            current_version: Current application version
        """
        self.repo_owner = repo_owner
        self.repo_name = repo_name
        self.current_version = current_version
        self.github_api_url = f"https://api.github.com/repos/{repo_owner}/{repo_name}/releases/latest"
        
    def get_current_version(self):
        """Get the current application version."""
        return self.current_version
    
    def fetch_latest_release(self):
        """
        Fetch the latest release information from GitHub.
        
        Returns:
            dict: Release information or None if failed
        """
        try:
            headers = {
                'Accept': 'application/vnd.github.v3+json',
                'User-Agent': 'CTe-LogLife-Updater'
            }
            
            response = requests.get(self.github_api_url, headers=headers, timeout=10)
            
            if response.status_code == 200:
                return response.json()
            else:
                print(f"Failed to fetch release info. Status code: {response.status_code}")
                return None
                
        except requests.exceptions.RequestException as e:
            print(f"Error fetching release information: {e}")
            return None
    
    def parse_version(self, version_string):
        """
        Parse version string to Version object.
        
        Args:
            version_string: Version string (e.g., "v1.2.3" or "1.2.3")
            
        Returns:
            Version object
        """
        # Remove 'v' prefix if present
        clean_version = version_string.lstrip('v')
        return version.parse(clean_version)
    
    def check_for_updates(self):
        """
        Check if a new version is available.
        
        Returns:
            dict: Update information with keys:
                - update_available (bool): Whether an update is available
                - latest_version (str): Latest version string
                - current_version (str): Current version string
                - download_url (str): URL to download the installer
                - release_notes (str): Release notes/description
                - release_name (str): Release name
        """
        release_info = self.fetch_latest_release()
        
        if not release_info:
            return {
                'update_available': False,
                'error': 'Failed to fetch release information'
            }
        
        try:
            latest_version_str = release_info.get('tag_name', '').lstrip('v')
            release_name = release_info.get('name', 'New Version')
            release_notes = release_info.get('body', 'No release notes available.')
            
            # Get download URL for the installer
            download_url = None
            assets = release_info.get('assets', [])
            
            # Look for installer executable
            for asset in assets:
                asset_name = asset.get('name', '').lower()
                if asset_name.endswith('.exe') and 'setup' in asset_name or 'installer' in asset_name:
                    download_url = asset.get('browser_download_url')
                    break
            
            # If no specific installer found, use the first .exe asset
            if not download_url:
                for asset in assets:
                    if asset.get('name', '').lower().endswith('.exe'):
                        download_url = asset.get('browser_download_url')
                        break
            
            # Compare versions
            current_ver = self.parse_version(self.current_version)
            latest_ver = self.parse_version(latest_version_str)
            
            update_available = latest_ver > current_ver
            
            return {
                'update_available': update_available,
                'latest_version': latest_version_str,
                'current_version': self.current_version,
                'download_url': download_url,
                'release_notes': release_notes,
                'release_name': release_name,
                'needs_download': download_url is None and update_available
            }
            
        except Exception as e:
            print(f"Error parsing release information: {e}")
            return {
                'update_available': False,
                'error': f'Error parsing release: {str(e)}'
            }
    
    def get_version_info(self):
        """
        Get formatted version information.
        
        Returns:
            str: Formatted version information
        """
        update_info = self.check_for_updates()
        
        if update_info.get('error'):
            return f"Current Version: {self.current_version}\nError: {update_info['error']}"
        
        if update_info['update_available']:
            return (f"Current Version: {update_info['current_version']}\n"
                   f"Latest Version: {update_info['latest_version']}\n"
                   f"Status: Update Available!")
        else:
            return (f"Current Version: {update_info['current_version']}\n"
                   f"Status: Up to date")


if __name__ == "__main__":
    # Test the version checker
    checker = VersionChecker(current_version="1.0.0")
    update_info = checker.check_for_updates()
    
    print("=== Version Check ===")
    print(f"Current Version: {update_info.get('current_version', 'Unknown')}")
    print(f"Latest Version: {update_info.get('latest_version', 'Unknown')}")
    print(f"Update Available: {update_info.get('update_available', False)}")
    
    if update_info.get('download_url'):
        print(f"Download URL: {update_info['download_url']}")
    
    if update_info.get('release_notes'):
        print(f"\nRelease Notes:\n{update_info['release_notes']}")

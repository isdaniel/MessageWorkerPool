
```powershell
#step 1
python -m venv myenv

#step2
myenv\Scripts\activate

#step3
pip install -r requirements.txt

#setp5
$envFilePath = "../env/.local_env"
if (Test-Path $envFilePath) {
     Get-Content $envFilePath | ForEach-Object {
         if ($_ -match "^\s*([^#][^=]+?)\s*=\s*(.+)\s*$") {
             $key = $matches[1]
             $value = $matches[2]

     if ($value.StartsWith('"') -and $value.EndsWith('"')) {
                 $value = $value.Substring(1, $value.Length - 2)
             }

             Set-Item -Path Env:\$key -Value $value
             Write-Host "Set environment variable: $key=$value"
         }
     }
} else {
    Write-Host "Error: .env file not found."
}

#step5
pytest -v .\integration_test.py

```



Only build the customer pricing project.
Code is somthing like this:

& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe" "C:\Program Files\Acumatica ERP\AcumaticaArt\App_Data\Projects\CustomerPricing\CustomerPricing\CustomerPricing.csproj" /p:Configuration=Debug /v:normal

This is an acumatica specific project
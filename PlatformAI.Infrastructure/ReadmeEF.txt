dotnet tool install --global dotnet-ef
dotnet add package Microsoft.EntityFrameworkCore.Design


cd /Users/marcobazzoli/tmp/Claude/PlatformAI
dotnet ef migrations add AddChartsJsonToMessage --project PlatformAI.Infrastructure --startup-project PlatformAI --context ApplicationContext
dotnet ef database update --project PlatformAI.Infrastructure --startup-project PlatformAI --context ApplicationContext
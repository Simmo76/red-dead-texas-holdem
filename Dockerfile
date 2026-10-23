# One container that serves the Blazor web app and the multiplayer relay hub
# on a single port, so a free host (Render, Hugging Face Spaces, ...) can run
# the friends beta behind one permanent HTTPS URL.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY DotNet/ DotNet/
COPY Web/ Web/
COPY Assets/Scripts/ Assets/Scripts/
RUN dotnet publish Web/CinematicPoker.Web -c Release -o /out/web \
 && dotnet publish Web/CinematicPoker.Server -c Release -o /out/server

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out/server ./server
COPY --from=build /out/web/wwwroot ./wwwroot
ENV DMH_WEB_ROOT=/app/wwwroot
EXPOSE 10000
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet server/CinematicPoker.Server.dll"]

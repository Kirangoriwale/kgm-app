FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       libfontconfig1 \
       libfreetype6 \
       libharfbuzz0b \
       libpng16-16 \
       libjpeg62-turbo \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out ./

ENV PORT=10000
ENV ASPNETCORE_URLS=http://+:${PORT}

ENTRYPOINT ["dotnet", "KgmApp.dll"]
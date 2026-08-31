FROM node:22-alpine AS web-build
WORKDIR /src/web

COPY web/package.json web/package-lock.json ./
RUN npm ci

COPY web/ ./

ARG TURNSTILE_SITE_KEY=
ENV TURNSTILE_SITE_KEY=$TURNSTILE_SITE_KEY
RUN node -e "const fs=require('fs');const k=process.env.TURNSTILE_SITE_KEY||'';const esc=k.replace(/\\/g,'\\\\').replace(/'/g,'\\''');fs.writeFileSync('src/environments/environment.production.ts',`export const environment = {\n  production: true,\n  apiBaseUrl: '/api/v1',\n  turnstileSiteKey: '${esc}',\n};\n`)" \
    && npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY contracts/ contracts/
COPY api/ api/

WORKDIR /src/api
RUN dotnet restore Amanah.Api.csproj \
    && dotnet publish Amanah.Api.csproj -c Release -o /app/publish --no-restore

COPY --from=web-build /src/web/dist/web/browser /app/publish/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=api-build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Amanah.Api.dll"]

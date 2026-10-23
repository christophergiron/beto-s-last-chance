# beto-s-last-chance

Juego narrativo desarrollado con Blazor WebAssembly y .NET 10.

## Ejecutar localmente

```powershell
dotnet run --project Beto.Web/Beto.Web.csproj
```

Abre http://localhost:5290.

## Publicar en GitHub Pages

1. Sube a la rama `main` los archivos de este repositorio, incluido `.github/workflows/deploy-pages.yml`.
2. En GitHub, abre **Settings → Pages → Build and deployment → Source** y selecciona **GitHub Actions**. Necesitas permisos de administración del repositorio para cambiar esta opción.
3. Abre **Actions → Publicar Beto en GitHub Pages → Run workflow**, selecciona `main` y ejecuta el flujo. Los siguientes cambios enviados a `main` también ejecutarán la publicación.
4. Cuando los trabajos `build` y `deploy` terminen correctamente, abre el enlace del despliegue. Para este repositorio, sin dominio personalizado, será https://christophergiron.github.io/beto-s-last-chance/.

El flujo instala .NET 10, ejecuta las pruebas, publica `Beto.Web`, ajusta la ruta base según la configuración de Pages y sube únicamente los archivos de `wwwroot`. La configuración local conserva la ruta `/`. No requiere un token personal ni un servidor .NET permanente.

Si no tienes permisos para publicar en el repositorio original, crea un fork en tu cuenta, sube allí los cambios y activa Pages en ese fork. La dirección utilizará tu usuario; la ruta base se calcula automáticamente.

### Comprobar la compilación de publicación localmente

```powershell
dotnet publish Beto.Web/Beto.Web.csproj --configuration Release --output artifacts/site
./scripts/Prepare-GitHubPages.ps1 -PublishPath artifacts/site/wwwroot -BasePath /beto-s-last-chance/
```

La carpeta `artifacts/site/wwwroot` contiene el sitio compilado. Las partidas permanecen en el almacenamiento local del navegador y no se sincronizan entre dispositivos. La versión publicada y la versión de localhost tienen almacenamientos separados.

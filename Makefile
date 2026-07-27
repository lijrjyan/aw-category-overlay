.PHONY: build test package clean

PROJECT := src/ActivityWatch.CategoryOverlay.Windows/ActivityWatch.CategoryOverlay.Windows.csproj
SOLUTION := ActivityWatch.CategoryOverlay.sln

build:
	dotnet build $(SOLUTION) --configuration Release

test:
	dotnet test $(SOLUTION) --configuration Release

package:
	rm -rf dist
	dotnet publish $(PROJECT) \
		--configuration Release \
		--runtime win-x64 \
		--self-contained false \
		-p:PublishSingleFile=true \
		--output dist/publish
	cp dist/publish/ActivityWatch.CategoryOverlay.exe \
		dist/aw-category-overlay.exe

clean:
	dotnet clean $(SOLUTION)
	rm -rf dist

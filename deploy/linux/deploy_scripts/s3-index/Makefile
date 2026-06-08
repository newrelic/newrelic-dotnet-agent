GO := go
BINARIES := indexer

all: deps
	$(GO) build -o bin/indexer ./src/indexer

clean:
	rm -rf bin pkg

deps:
	$(GO) mod download

test: deps
	$(GO) test ./...

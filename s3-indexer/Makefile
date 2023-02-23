export GOPATH := $(shell pwd)
export GO111MODULE := auto

all: deps linux windows

linux: deps
	go build -o bin/linux/indexer indexer

windows: deps
	GOOS=windows GOARCH=amd64 go build -o bin/windows/indexer.exe indexer

clean:
	rm -f bin/linux/indexer bin/windows/indexer.exe
	rm -rf pkg src/github.com

deps:
	go get github.com/aws/aws-sdk-go

test: deps
	go test indexer s3/indexer

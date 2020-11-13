package main

import (
	"bytes"
	"flag"
	"html/template"
	"log"

	"github.com/aws/aws-sdk-go/aws/session"
	"github.com/aws/aws-sdk-go/service/s3"
	"github.com/aws/aws-sdk-go/service/s3/s3manager"

	"s3/indexer"
)

var (
	bucket string
	prefix string
	upload bool
)

const (
	ContentType   = "text/html; charset=UTF-8"
	IndexFileName = "index.html"
)

func indexDirectory(t *template.Template, mgr *s3manager.Uploader, dir *indexer.Directory) error {
	content, err := dir.Index(t)
	if err != nil {
		return err
	}

	target := dir.Prefix + "/" + IndexFileName
	log.Printf("Uploading index with %d dir(s) and %d file(s) for %s/ to %s\n", len(dir.Directories), len(dir.Files), dir.Prefix, target)

	if upload {
		contentType := ContentType
		input := &s3manager.UploadInput{
			Bucket:      &bucket,
			Key:         &target,
			Body:        bytes.NewReader(content),
			ContentType: &contentType,
		}
		if _, err := mgr.Upload(input); err != nil {
			return err
		}
	}

	return nil
}

func handleDirectory(t *template.Template, mgr *s3manager.Uploader, dir *indexer.Directory) {
	if dir.ShouldIndex(prefix) {
		if err := indexDirectory(t, mgr, dir); err != nil {
			log.Println("Error indexing directory %s: %v", dir.Prefix, err)
		}
	}

	for _, d := range dir.Directories {
		handleDirectory(t, mgr, d)
	}
}

func main() {
	flag.StringVar(&bucket, "bucket", "", "the S3 bucket")
	flag.StringVar(&prefix, "prefix", "", "the prefix to enumerate")
	flag.BoolVar(&upload, "upload", false, "true to actually upload the index files")
	flag.Parse()

	if !upload {
		log.Println("-upload not given; just telling you what would happen instead of actually doing it")
	}

	t := template.New("index")
	t, err := t.Parse(indexer.DefaultTemplate)
	if err != nil {
		log.Fatalln(err)
	}

	sess, err := session.NewSessionWithOptions(session.Options{
		SharedConfigState: session.SharedConfigEnable,
	})
	if err != nil {
		log.Fatalln(err)
	}

	svc := s3.New(sess)
	mgr := s3manager.NewUploader(sess)

	top := indexer.NewTopDirectory()
	if err := top.Enumerate(svc, bucket, prefix); err != nil {
		log.Fatalln(err)
	}

	handleDirectory(t, mgr, top)
}

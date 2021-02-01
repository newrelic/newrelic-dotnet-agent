package indexer

import (
	"bytes"
	"html/template"
	"strings"
	"time"

	"github.com/aws/aws-sdk-go/aws"
	"github.com/aws/aws-sdk-go/service/s3"
)

type Directory struct {
	Parent       *Directory
	Prefix       string
	Name         string
	LastModified time.Time
	Directories  map[string]*Directory
	Files        map[string]*File
}

func NewDirectory(prefix, name string, parent *Directory) *Directory {
	return &Directory{
		Parent:       parent,
		Prefix:       prefix,
		Name:         name,
		LastModified: time.Unix(0, 0),
		Directories:  make(map[string]*Directory),
		Files:        make(map[string]*File),
	}
}

func NewTopDirectory() *Directory {
	return NewDirectory("", "", nil)
}

func (d *Directory) Enumerate(svc *s3.S3, bucket, prefix string) error {
	params := &s3.ListObjectsV2Input{
		Bucket: aws.String(bucket),
		Prefix: aws.String(prefix),
	}
	if err := svc.ListObjectsV2Pages(params, func(resp *s3.ListObjectsV2Output, lastPage bool) bool {
		for _, obj := range resp.Contents {
			file := NewFile(obj)

			// Special case: ignore index.html when generating directories.
			if file.Name == "index.html" {
				continue
			}

			// Split up the key and create the directory structure as required within
			// the nested maps.
			parts := strings.Split(*obj.Key, "/")
			ctx := d
			for i, part := range parts[:len(parts)-1] {
				if ctx.LastModified.Before(*file.LastModified) {
					ctx.LastModified = *file.LastModified
				}

				if child, ok := ctx.Directories[part]; ok {
					ctx = child
				} else {
					ctx.Directories[part] = NewDirectory(strings.Join(parts[0:i+1], "/"), part, ctx)
					ctx = ctx.Directories[part]
				}
			}

			if ctx.LastModified.Before(*file.LastModified) {
				ctx.LastModified = *file.LastModified
			}

			ctx.Files[file.Name] = file
		}

		return true
	}); err != nil {
		return err
	}

	return nil
}

func (d *Directory) Index(t *template.Template) ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := t.Execute(buf, d); err != nil {
		return nil, err
	}

	return buf.Bytes(), nil
}

func (d *Directory) ShouldIndex(prefix string) bool {
	if _, ok := d.Files[".noindex"]; ok {
		return false
	}

	if strings.Index(strings.TrimRight(d.Prefix, "/"), strings.TrimRight(prefix, "/")) != 0 {
		return false
	}

	return true
}

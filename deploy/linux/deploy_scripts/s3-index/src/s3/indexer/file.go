package indexer

import (
	"path"

	"github.com/aws/aws-sdk-go-v2/service/s3/types"
)

type File struct {
	types.Object
	Name string
}

func NewFile(obj types.Object) *File {
	return &File{
		Object: obj,
		Name:   path.Base(*obj.Key),
	}
}

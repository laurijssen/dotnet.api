```
#!/bin/bash

# script for creating new solution with one project
# OR
# add project to existing solution
# 
# This command:
# ./dotnetprojects.sh new /c/git microservices newproject
# will create microservices.sln with project  newproject.api + .client + .unittest

if [ $# -lt 5 ]; then
    echo "newproject: arguments <cmd>(new|add) <rootdir> <solution> <projectname> <type>(grpc|webapi)"
    echo "exiting"
    exit 1
fi

function new()
{    
	if [ ! -d ${solution} ]; then
		mkdir ${solution}
	fi

    popd

    pushd ${root}/${solution}

    mkdir -p src/${api}/models
    mkdir -p services/${service}
    mkdir -p tests/${tests}
	
	mkdir -p clients/${client}
	mkdir -p shared/${shared}

    if [ ! -d docs ]; then
        mkdir docs
    fi

    touch "README.md"

    if [ ${command} = "new" ]; then
        dotnet new sln --name=${solution}
    fi

	pushd clients
	dotnet new classlib --name="${client}"
	popd

	if [ ${type} != "grpc" ]; then	
		pushd shared
		dotnet new classlib --name="${shared}" --target-framework-override net48	
		popd
	else
		pushd shared
		dotnet new classlib --name="${shared}"
		popd	
	fi

	pushd services
	
	dotnet new classlib --name="${service}"
	
	popd

    pushd src	
	
    dotnet new ${type} --name="${api}"
	
	pushd ${api}
	
	dotnet add reference "../../services/${service}"
	
	popd
	
    popd

    pushd tests
    
    dotnet new xunit --name="${tests}"

    pushd ${tests}

    dotnet add reference "../../services/${service}"
	dotnet add reference "../../src/${api}"	
	dotnet add reference "../../clients/${client}"
	dotnet add reference "../../shared/${shared}"	
	
    popd

    popd

    dotnet sln add "services/${service}"
    dotnet sln add "src/${api}"
    dotnet sln add "tests/${tests}"
	
	dotnet sln add "clients/${client}"
	dotnet sln add "shared/${shared}"
}

function initgit()
{
	gh repo create fujifilmimagingproductsandsolutions/${solution} --private

	if [ $? -ne 0 ]; then
		echo "could not create private github repo ${solution}"
	#	exit 1
	fi

	dotnet new gitignore
	git init
	git add README.md
	git add .
	git commit -m "initial commit"
	git branch -M main
	git remote add origin https://github.com/FujifilmImagingProductsAndSolutions/${solution}.git	
	git push -u origin main
}

function addgit()
{
	git add .
	git commit -am "added ${project} to ${solution}"
	git push
}

type=$5
project=$4
solution=$3
root=$2
command=$1

if [[ ${type} != "grpc" && ${type} != "webapi" ]];
then
	echo "type argument must be either grpc or webapi, not ${type}"
	exit 1
fi

api=${project}.api
tests=${project}.unittests
client=${project}.client
service=${project}.service
shared=${project}.shared

# a weird git bash problem where non printable characters appeared in fron of $solution, so remove these
solution=$(echo ${solution} | tr -cd '[:print:]')

pushd ${root}

if [ "${command}" = "new" ]; then
    new
	initgit
fi

if [ "${command}" = "add" ]; then
    new
	#addgit
fi

popd
```

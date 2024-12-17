    #!/bin/bash

if [ $# -lt 4 ]; then
    echo "newproject: arguments <cmd>(new|add) <rootdir> <solution> <projectname>"
    echo "exiting"
    exit 1
fi

function new()
{
    pushd ${root}

    mkdir ${solution}

    popd

    pushd ${root}/${solution}

    mkdir -p src/${api}/models
    mkdir -p src/${api}/services
    mkdir -p tests/${tests}
    mkdir -p clients/${client}

    if [ ! -d shared ]; then
        mkdir shared
    fi

    if [ ! -d docs ]; then
        mkdir docs
    fi

    touch "README.md"

    if [ ${command} = "new" ]; then
        dotnet new sln --name=${solution}
    fi

    pushd src

    dotnet new webapi --name="${api}"

    popd

    pushd clients

    dotnet new classlib --name="${client}"

    popd

    pushd tests
    
    dotnet new xunit --name="${tests}"

    pushd ${tests}

    dotnet add reference "../../clients/${client}"
    dotnet add reference "../../src/${api}"

    popd

    popd

    dotnet sln add "clients/${client}"
    dotnet sln add "src/${api}"
    dotnet sln add "tests/${tests}"

    popd
}

project=$4
solution=$3
root=$2
command=${1}
api=${project}.api
tests=${project}.unittests
client=${project}.client

if [ "${command}" = "new" ]; then
    new
fi

if [ "${command}" = "add" ]; then
    new
fi

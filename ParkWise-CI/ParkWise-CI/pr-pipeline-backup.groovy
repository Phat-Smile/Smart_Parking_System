
pipeline {
    agent {
        label 'parkwise-pr'
    }

    options {
        skipDefaultCheckout(true)
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 60, unit: 'MINUTES')
    }

    stages {
        stage('Verify Pull Request') {
            steps {
                script {
                    if (!env.CHANGE_ID) {
                        error('This pipeline only supports Pull Requests.')
                    }

                    if (env.CHANGE_TARGET != 'develop') {
                        error(
                            "Expected PR targeting develop, " +
                            "got '${env.CHANGE_TARGET}'"
                        )
                    }

                    echo "PR Number: ${env.CHANGE_ID}"
                    echo "PR Source: ${env.CHANGE_BRANCH}"
                    echo "PR Target: ${env.CHANGE_TARGET}"
                }
            }
        }

        stage('Checkout PR') {
            steps {
                checkout scm
                sh 'git log -1 --oneline'
            }
        }

        stage('Verify .NET SDK') {
            steps {
                sh 'dotnet --version'
            }
        }

        stage('Restore') {
            steps {
                sh '''
                    dotnet restore ParkingSystem.slnx \
                        -m:1 \
                        --disable-build-servers
                '''
            }
        }

        stage('Build') {
            steps {
                sh '''
                    dotnet build ParkingSystem.slnx \
                        --no-restore \
                        -m:1 \
                        --disable-build-servers
                '''
            }
        }

        stage('Unit Tests') {
            steps {
                sh '''
                    set -e

                    dotnet test tests/Identity/Identity.UnitTests/Identity.UnitTests.csproj --no-build --no-restore

                    dotnet test tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj --no-build --no-restore

                    dotnet test tests/Booking/Booking.UnitTests/Booking.UnitTests.csproj --no-build --no-restore

                    dotnet test tests/Payment/Payment.UnitTests/Payment.UnitTests.csproj --no-build --no-restore

                    dotnet test tests/Notification/Notification.UnitTests/Notification.UnitTests.csproj --no-build --no-restore
                '''
            }
        }

        stage('Architecture Tests') {
            steps {
                sh '''
                    dotnet test tests/ArchitectureTests/ArchitectureTests.csproj \
                        --no-build \
                        --no-restore
                '''
            }
        }

        stage('Web Foundation Tests') {
            steps {
                sh '''
                    dotnet test tests/WebFoundationTests/WebFoundationTests.csproj \
                        --no-build \
                        --no-restore
                '''
            }
        }
    }

    post {
        success {
            echo 'ParkWise PR CI: SUCCESS'
        }

        failure {
            echo 'ParkWise PR CI: FAILED'
        }
    }
}
